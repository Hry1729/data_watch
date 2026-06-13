"""一次性清理脚本：删除数据库中残留的演示菜单（及其子菜单、角色-菜单关联）。

背景：菜单种子 _seed_menus 是「只增不改不删」，早期 bootstrap 已把 art-design-pro
的演示菜单（dashboard/template/widgets/examples/article/safeguard/change-log）灌入
sys_menu。前端 clean:dev 删掉了对应页面文件后，注册路由时这些菜单会报「未找到组件」。
本脚本把这些演示菜单从数据库删除，只保留 device_monitor / system / result / exception。

用法（在 backend 目录下，使用后端同一 Python 环境）：
    python scripts/clean_demo_menus.py

幂等：重复运行时若已无演示菜单，会提示已干净并退出。
"""

import asyncio
import sys
from pathlib import Path

# 确保无论从哪个目录运行都能 import app.*
BACKEND_DIR = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(BACKEND_DIR))

from sqlalchemy import delete, select  # noqa: E402

from app.db.mysql import get_mysql_session_factory  # noqa: E402
from app.models.auth import SysMenu, SysPermission, SysRoleMenu  # noqa: E402

# 要删除的演示菜单顶层名称（其子菜单按 parent_id 递归收集）
DEMO_ROOT_NAMES = [
    "Dashboard",
    "Template",
    "Widgets",
    "Examples",
    "Article",
    "Safeguard",
    "ChangeLog",
]


async def main() -> None:
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        # 1. 读取全部菜单，构建 parent_id -> children 映射
        menus = (await session.scalars(select(SysMenu))).all()
        children_by_parent: dict[int | None, list[SysMenu]] = {}
        by_id: dict[int, SysMenu] = {}
        for menu in menus:
            by_id[menu.id] = menu
            children_by_parent.setdefault(menu.parent_id, []).append(menu)

        # 2. 找演示根菜单
        roots = [menu for menu in menus if menu.name in DEMO_ROOT_NAMES]
        if not roots:
            print("未发现演示菜单，数据库已是干净状态，无需处理。")
            return

        # 3. BFS 收集根 + 所有子孙的 id
        to_delete: list[int] = []
        stack: list[int] = [menu.id for menu in roots]
        while stack:
            menu_id = stack.pop()
            to_delete.append(menu_id)
            for child in children_by_parent.get(menu_id, []):
                stack.append(child.id)

        print(f"将删除 {len(to_delete)} 个演示菜单（含子菜单）：")
        for menu in sorted((by_id[i] for i in to_delete), key=lambda m: m.id):
            print(f"  - id={menu.id:<4} name={menu.name:<24} path={menu.path}")

        # 4. 先删角色-菜单关联与（若有）菜单权限，再删菜单本身
        await session.execute(
            delete(SysRoleMenu).where(SysRoleMenu.menu_id.in_(to_delete))
        )
        await session.execute(
            delete(SysPermission).where(SysPermission.menu_id.in_(to_delete))
        )
        await session.execute(delete(SysMenu).where(SysMenu.id.in_(to_delete)))
        await session.commit()

        print(f"完成：已删除 {len(to_delete)} 个演示菜单及其角色关联。")


if __name__ == "__main__":
    asyncio.run(main())
