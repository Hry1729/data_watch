"""健康检查"""

import asyncio
from typing import Dict

from core.logger import get_logger
from device.manager import DeviceManager
from storage.tdengine import TDengineConnection

logger = get_logger("health")


class HealthChecker:
    """系统健康检查"""

    def __init__(
        self,
        device_manager: DeviceManager,
        tdengine: TDengineConnection,
        interval: int = 30,
    ):
        self.device_manager = device_manager
        self.tdengine = tdengine
        self.interval = interval
        self._running = False

    async def run(self) -> None:
        """定期检查健康状态"""
        self._running = True
        logger.info("HealthChecker started")

        while self._running:
            try:
                status = await self.check()
                logger.info(f"Health status: {status}")
            except Exception as e:
                logger.error(f"Health check error: {e}")

            await asyncio.sleep(self.interval)

    async def check(self) -> Dict:
        """执行一次健康检查"""
        status = {
            "tdengine": "ok" if self.tdengine.is_connected else "disconnected",
            "devices": {},
        }

        for device in self.device_manager.get_all_devices():
            healthy = await self.device_manager.check_device_health(device)
            if not healthy:
                await self.device_manager.schedule_reconnect(device)
            status["devices"][device.device_id] = {
                "status": device.status.value,
                "protocol": device.protocol,
                "healthy": healthy,
            }

        online = sum(
            1
            for d in self.device_manager.get_all_devices()
            if d.status.value == "online"
        )
        total = len(self.device_manager.get_all_devices())
        status["summary"] = f"{online}/{total} devices online"

        return status

    def stop(self) -> None:
        self._running = False
