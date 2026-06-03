"""OPC DA tag 到 variable_id 的映射器

用于将 OPC DA sidecar 推送的 item_id 映射到 MySQL 中的 variable_id。
映射逻辑：var_address (完整 item_id) → variable_id
"""

from typing import Dict, List, Optional

from core.logger import get_logger

logger = get_logger("opcda_mapper")


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
        self._mapping: Dict[str, str] = {}
        self._device_variables: Dict[str, List[str]] = {}  # device_id → [variable_id]
        self.rebuild(variables)

    def rebuild(self, variables: List) -> None:
        """
        用新的变量列表原地重建映射（热重载后调用）。

        原地更新内部 dict 引用，使已注入到 ingest server 的同一 mapper 引用
        立即生效；不要替换 mapper 对象本身。
        """
        mapping: Dict[str, str] = {}
        device_variables: Dict[str, List[str]] = {}

        for var in variables:
            # var.address 存储完整的 item_id
            address = getattr(var, "address", "")
            variable_id = getattr(var, "variable_id", "")
            device_id = getattr(var, "device_id", "")

            if address and variable_id:
                mapping[address] = variable_id
                device_variables.setdefault(device_id, []).append(variable_id)

        # 一次性替换引用，避免读取方看到半更新状态
        self._mapping = mapping
        self._device_variables = device_variables

        logger.info(
            "OpcDaTagMapper rebuilt: %d mappings, %d devices",
            len(self._mapping),
            len(self._device_variables),
        )

    def get_variable_id(self, item_id: str) -> Optional[str]:
        """
        根据 OPC DA item_id 查找 variable_id

        Args:
            item_id: 完整的 OPC DA item ID，如 "OPD_Server.AGV801.ViewMain.Tag30041800"

        Returns:
            variable_id 或 None（未找到）
        """
        return self._mapping.get(item_id)

    def get_all_mappings(self) -> Dict[str, str]:
        """获取所有映射关系（用于调试）"""
        return self._mapping.copy()

    def get_device_variables(self, device_id: str) -> List[str]:
        """获取指定设备的所有 variable_id"""
        return self._device_variables.get(device_id, [])

    @property
    def mapping_count(self) -> int:
        """当前映射数量"""
        return len(self._mapping)

    @property
    def device_count(self) -> int:
        """当前设备数量"""
        return len(self._device_variables)
