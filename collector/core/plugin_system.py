"""插件系统"""

import importlib
import inspect
import os
from abc import ABC, abstractmethod
from typing import Any, Dict, List, Optional, Type, Callable
from dataclasses import dataclass

from core.logger import get_logger
from drivers.base import BaseDriver, ReadBlock, WritePoint
from core.exceptions import DeviceConnectionError

logger = get_logger("plugin_system")


@dataclass
class PluginInfo:
    """插件信息"""
    name: str
    version: str
    description: str
    author: str
    dependencies: List[str]
    enabled: bool = True


class Plugin(ABC):
    """插件基类"""

    @property
    @abstractmethod
    def info(self) -> PluginInfo:
        """获取插件信息"""
        pass

    @abstractmethod
    def initialize(self, config: Dict[str, Any]) -> None:
        """初始化插件"""
        pass

    @abstractmethod
    def cleanup(self) -> None:
        """清理插件"""
        pass


class DriverPlugin(Plugin):
    """驱动插件基类"""

    @abstractmethod
    def get_driver_class(self) -> Type[BaseDriver]:
        """获取驱动类"""
        pass

    @abstractmethod
    def get_default_config(self) -> Dict[str, Any]:
        """获取默认配置"""
        pass


class ProtocolRegistry:
    """协议注册表"""

    def __init__(self):
        self._protocols: Dict[str, DriverPlugin] = {}
        self._protocol_classes: Dict[str, Type[BaseDriver]] = {}
        self._loaders: Dict[str, Callable] = {}

    def register_protocol(self, name: str, plugin: DriverPlugin):
        """注册协议"""
        self._protocols[name] = plugin
        self._protocol_classes[name] = plugin.get_driver_class()
        logger.info(f"Registered protocol: {name}")

    def register_loader(self, protocol_name: str, loader: Callable):
        """注册协议加载器"""
        self._loaders[protocol_name] = loader
        logger.info(f"Registered loader for protocol: {protocol_name}")

    def get_protocol(self, name: str) -> Optional[DriverPlugin]:
        """获取协议插件"""
        return self._protocols.get(name)

    def get_driver_class(self, name: str) -> Optional[Type[BaseDriver]]:
        """获取驱动类"""
        return self._protocol_classes.get(name)

    def list_protocols(self) -> List[str]:
        """列出所有协议"""
        return list(self._protocols.keys())

    def load_protocol_from_file(self, file_path: str, protocol_name: str) -> bool:
        """从文件加载协议"""
        try:
            # 动态导入模块
            spec = importlib.util.spec_from_file_location("protocol_module", file_path)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)

            # 查找驱动类
            for name, obj in inspect.getmembers(module):
                if (inspect.isclass(obj) and
                    issubclass(obj, BaseDriver) and
                    obj != BaseDriver):

                    # 创建插件
                    class PluginWrapper(DriverPlugin):
                        def __init__(self, driver_class):
                            self._driver_class = driver_class

                        @property
                        def info(self):
                            return PluginInfo(
                                name=protocol_name,
                                version="1.0.0",
                                description=f"Driver for {protocol_name}",
                                author="Unknown",
                                dependencies=[]
                            )

                        def initialize(self, config):
                            pass

                        def cleanup(self):
                            pass

                        def get_driver_class(self):
                            return self._driver_class

                        def get_default_config(self):
                            return {}

                    plugin = PluginWrapper(obj)
                    self.register_protocol(protocol_name, plugin)
                    logger.info(f"Loaded protocol from {file_path}: {protocol_name}")
                    return True

            logger.error(f"No valid driver class found in {file_path}")
            return False

        except Exception as e:
            logger.error(f"Failed to load protocol from {file_path}: {e}")
            return False

    def discover_plugins(self, plugin_dir: str) -> List[str]:
        """发现插件目录中的协议"""
        discovered = []

        if not os.path.exists(plugin_dir):
            logger.warning(f"Plugin directory not found: {plugin_dir}")
            return discovered

        for filename in os.listdir(plugin_dir):
            if filename.endswith('.py') and not filename.startswith('__'):
                protocol_name = filename[:-3]  # 去掉.py
                file_path = os.path.join(plugin_dir, filename)

                if self.load_protocol_from_file(file_path, protocol_name):
                    discovered.append(protocol_name)

        return discovered


class PluginManager:
    """插件管理器"""

    def __init__(self):
        self.protocol_registry = ProtocolRegistry()
        self._plugins: Dict[str, Plugin] = {}
        self._plugin_configs: Dict[str, Dict] = {}

    def load_plugins_from_directory(self, directory: str) -> int:
        """从目录加载所有插件"""
        loaded_count = 0

        if not os.path.exists(directory):
            logger.warning(f"Plugin directory not found: {directory}")
            return 0

        # 加载驱动插件
        drivers_dir = os.path.join(directory, "drivers")
        if os.path.exists(drivers_dir):
            protocols = self.protocol_registry.discover_plugins(drivers_dir)
            loaded_count += len(protocols)

        # 加载其他类型插件
        for filename in os.listdir(directory):
            if filename.endswith('.py') and not filename.startswith('__'):
                plugin_name = filename[:-3]
                file_path = os.path.join(directory, filename)

                try:
                    spec = importlib.util.spec_from_file_location(f"plugin_{plugin_name}", file_path)
                    module = importlib.util.module_from_spec(spec)
                    spec.loader.exec_module(module)

                    # 查找插件类
                    for name, obj in inspect.getmembers(module):
                        if (inspect.isclass(obj) and
                            issubclass(obj, Plugin) and
                            obj != Plugin):

                            try:
                                plugin = obj()
                                self.register_plugin(plugin_name, plugin)
                                loaded_count += 1
                            except Exception as e:
                                logger.error(f"Failed to instantiate plugin {plugin_name}: {e}")

                except Exception as e:
                    logger.error(f"Failed to load plugin {filename}: {e}")

        logger.info(f"Loaded {loaded_count} plugins from {directory}")
        return loaded_count

    def register_plugin(self, name: str, plugin: Plugin):
        """注册插件"""
        self._plugins[name] = plugin
        logger.info(f"Registered plugin: {name} ({plugin.info.name})")

    def get_plugin(self, name: str) -> Optional[Plugin]:
        """获取插件"""
        return self._plugins.get(name)

    def list_plugins(self) -> List[Dict[str, Any]]:
        """列出所有插件"""
        return [
            {
                "name": name,
                "info": plugin.info,
                "enabled": plugin.info.enabled
            }
            for name, plugin in self._plugins.items()
        ]

    def enable_plugin(self, name: str) -> bool:
        """启用插件"""
        plugin = self._plugins.get(name)
        if plugin:
            plugin.info.enabled = True
            logger.info(f"Enabled plugin: {name}")
            return True
        return False

    def disable_plugin(self, name: str) -> bool:
        """禁用插件"""
        plugin = self._plugins.get(name)
        if plugin:
            plugin.info.enabled = False
            logger.info(f"Disabled plugin: {name}")
            return True
        return False

    def configure_plugin(self, name: str, config: Dict[str, Any]) -> bool:
        """配置插件"""
        plugin = self._plugins.get(name)
        if plugin:
            try:
                if hasattr(plugin, 'configure'):
                    plugin.configure(config)
                self._plugin_configs[name] = config
                logger.info(f"Configured plugin: {name}")
                return True
            except Exception as e:
                logger.error(f"Failed to configure plugin {name}: {e}")
                return False
        return False

    def initialize_all_plugins(self) -> None:
        """初始化所有插件"""
        for name, plugin in self._plugins.items():
            if plugin.info.enabled:
                try:
                    config = self._plugin_configs.get(name, {})
                    plugin.initialize(config)
                    logger.info(f"Initialized plugin: {name}")
                except Exception as e:
                    logger.error(f"Failed to initialize plugin {name}: {e}")

    def cleanup_all_plugins(self) -> None:
        """清理所有插件"""
        for name, plugin in self._plugins.items():
            if plugin.info.enabled:
                try:
                    plugin.cleanup()
                    logger.info(f"Cleaned up plugin: {name}")
                except Exception as e:
                    logger.error(f"Failed to cleanup plugin {name}: {e}")


# 全局插件管理器
_plugin_manager = None


def get_plugin_manager() -> PluginManager:
    """获取插件管理器"""
    global _plugin_manager
    if _plugin_manager is None:
        _plugin_manager = PluginManager()
    return _plugin_manager


def init_plugin_manager(plugin_dir: str = None) -> PluginManager:
    """初始化插件管理器"""
    global _plugin_manager
    _plugin_manager = PluginManager()

    if plugin_dir:
        _plugin_manager.load_plugins_from_directory(plugin_dir)

    return _plugin_manager