"""OPC DA tag 到 variable_id 的映射器

用于将 OPC DA sidecar 推送的 item_id 映射到 MySQL 中的 variable_id。
映射逻辑：(device_id, var_address 完整 item_id) → variable_id；
无 device_id 的旧客户端仅能路由全局唯一的 item_id。
"""

from typing import Dict, List, Optional, Set, Tuple

from core.logger import get_logger

logger = get_logger("opcda_mapper")


def normalize_device_id(device_id: Optional[object]) -> str:
    """OPC DA 推送里的 device_id 比较键：去首尾空白、大小写不敏感（casefold）。

    sidecar / 现场配置里设备编码大小写常不一致（AGV801 vs agv801），按大小写敏感
    匹配会把整批数据当作 unmapped 丢弃。mapper 与 ingest_server 必须共用此函数。
    """
    return str(device_id or "").strip().casefold()


class OpcDaTagMapper:
    """
    OPC DA tag 到 variable_id 的映射器

    映射关系：
    - key: var_address (完整的 OPC DA item_id，如 "OPD_Server.AGV801.ViewMain.Tag30041800")
    - value: variable_id (MySQL device_variable.id)
    """

    def __init__(self, variables: List):
        """
        初始化映射器

        Args:
            variables: VariableConfig 列表，从 MySQL 加载的点位配置
        """
        # B045：backend 只保证"同一设备内"地址唯一，不同设备（不同 OPC server 上同构命名）
        # 可以配置相同完整 ItemID ⇒ 以 (device_id, item_id) 为主键；
        # 仅全局唯一的 ItemID 才进入无设备兼容索引，重名的记入 _ambiguous。
        self._mapping: Dict[str, str] = {}
        self._device_mapping: Dict[Tuple[str, str], str] = {}
        self._folded_device_mapping: Dict[Tuple[str, str], str] = {}
        self._canonical_device: Dict[str, str] = {}  # normalize_device_id → 配置中的原始 device_id
        self._case_conflicts: Set[str] = set()
        self._ambiguous: Set[str] = set()
        self._device_variables: Dict[str, List[str]] = {}  # device_id → [variable_id]
        self.rebuild(variables)

    def rebuild(self, variables: List) -> None:
        """
        用新的变量列表原地重建映射（热重载后调用）。

        原地更新内部 dict 引用，使已注入到 ingest server 的同一 mapper 引用
        立即生效；不要替换 mapper 对象本身。
        """
        device_mapping: Dict[Tuple[str, str], str] = {}
        owners: Dict[str, Set[str]] = {}
        device_variables: Dict[str, List[str]] = {}

        for var in variables:
            # var.address 存储完整的 item_id
            address = getattr(var, "address", "")
            variable_id = getattr(var, "variable_id", "")
            device_id = str(getattr(var, "device_id", "") or "")

            if address and variable_id:
                device_mapping[(device_id, address)] = variable_id
                owners.setdefault(address, set()).add(variable_id)
                device_variables.setdefault(device_id, []).append(variable_id)

        mapping: Dict[str, str] = {}
        ambiguous: Set[str] = set()
        for (device_id, address), variable_id in device_mapping.items():
            if len(owners[address]) > 1:
                ambiguous.add(address)
            else:
                mapping[address] = variable_id

        # 设备 ID 大小写不敏感索引；若配置里存在仅大小写不同的两个设备 ID，
        # 该折叠键视为冲突，只接受与配置完全一致的写法，避免误路由。
        folded_owners: Dict[str, Set[str]] = {}
        for device_id in device_variables:
            folded_owners.setdefault(normalize_device_id(device_id), set()).add(device_id)
        case_conflicts = {key for key, ids in folded_owners.items() if len(ids) > 1}
        canonical_device = {
            key: next(iter(ids)) for key, ids in folded_owners.items() if key not in case_conflicts
        }
        folded_device_mapping: Dict[Tuple[str, str], str] = {
            (normalize_device_id(device_id), address): variable_id
            for (device_id, address), variable_id in device_mapping.items()
            if normalize_device_id(device_id) not in case_conflicts
        }

        # 一次性替换引用，避免读取方看到半更新状态
        self._mapping = mapping
        self._device_mapping = device_mapping
        self._folded_device_mapping = folded_device_mapping
        self._canonical_device = canonical_device
        self._case_conflicts = case_conflicts
        self._ambiguous = ambiguous
        self._device_variables = device_variables

        if case_conflicts:
            logger.warning(
                "OpcDaTagMapper: device_id(s) differ only by case %s; "
                "case-insensitive matching disabled for them",
                sorted(case_conflicts),
            )
        if ambiguous:
            logger.info(
                "OpcDaTagMapper: %d item_id(s) shared across devices; "
                "pushes for them must carry device_id",
                len(ambiguous),
            )
        logger.info(
            "OpcDaTagMapper rebuilt: %d mappings, %d devices",
            len(self._device_mapping),
            len(self._device_variables),
        )

    def get_variable_id(self, item_id: str, device_id: Optional[str] = None) -> Optional[str]:
        """
        根据 OPC DA item_id（及可选的 device_id）查找 variable_id

        Args:
            item_id: 完整的 OPC DA item ID，如 "OPD_Server.AGV801.ViewMain.Tag30041800"
            device_id: 推送方声明的设备 ID；提供时按 (device_id, item_id) 查找，
                device_id 大小写不敏感（见 normalize_device_id）

        Returns:
            variable_id 或 None（未找到；未带 device_id 且 item_id 跨设备重名时也返回 None，
            调用方可用 is_ambiguous() 区分）
        """
        device_key = str(device_id or "").strip()
        if device_key:
            exact = self._device_mapping.get((device_key, item_id))
            if exact is not None:
                return exact
            return self._folded_device_mapping.get((normalize_device_id(device_key), item_id))
        return self._mapping.get(item_id)

    def canonical_device_id(self, device_id: Optional[str]) -> Optional[str]:
        """把推送方的 device_id 还原为配置中的原始写法（大小写不敏感）；未知或大小写冲突返回 None。"""
        device_key = str(device_id or "").strip()
        if not device_key:
            return None
        if device_key in self._device_variables:
            return device_key
        return self._canonical_device.get(normalize_device_id(device_key))

    def is_ambiguous(self, item_id: str) -> bool:
        """item_id 是否被多个设备共用（无 device_id 时无法唯一路由）。"""
        return item_id in self._ambiguous

    def get_all_mappings(self) -> Dict[str, str]:
        """获取所有映射关系（用于调试）"""
        return self._mapping.copy()

    def get_device_variables(self, device_id: str) -> List[str]:
        """获取指定设备的所有 variable_id"""
        canonical = self.canonical_device_id(device_id)
        return self._device_variables.get(canonical or device_id, [])

    @property
    def mapping_count(self) -> int:
        """当前映射数量（按 (device_id, item_id) 计）"""
        return len(self._device_mapping)

    @property
    def device_count(self) -> int:
        """当前设备数量"""
        return len(self._device_variables)
