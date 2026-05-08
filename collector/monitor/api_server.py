"""监控API服务器"""

import asyncio
import time
from http.server import HTTPServer, BaseHTTPRequestHandler
import json
import urllib.parse
from typing import Any, Dict, Optional, TYPE_CHECKING
from urllib.parse import urlparse, parse_qs

from core.logger import get_logger
from monitor.health import HealthChecker
from monitor.metrics import get_metrics_collector

if TYPE_CHECKING:
    from device.manager import DeviceManager
    from core.config import CollectorConfig

logger = get_logger("api_server")


class MonitorAPIHandler(BaseHTTPRequestHandler):
    """监控API处理器"""

    # 类变量，由 MonitorAPIServer 设置
    device_manager: Optional["DeviceManager"] = None
    config: Optional["CollectorConfig"] = None

    def do_GET(self):
        """处理GET请求"""
        try:
            parsed_path = urlparse(self.path)
            path = parsed_path.path
            query = parse_qs(parsed_path.query)

            # 路由处理
            if path == '/health':
                self._handle_health()
            elif path == '/metrics':
                self._handle_metrics(query)
            elif path == '/devices':
                self._handle_devices(query)
            elif path == '/config':
                self._handle_config()
            elif path == '/':
                self._handle_index()
            else:
                self._send_error(404, "Not Found")

        except Exception as e:
            logger.error(f"API error: {e}")
            self._send_error(500, f"Internal Server Error: {str(e)}")

    def _send_json_response(self, status_code: int, data: Dict[str, Any]):
        """发送JSON响应"""
        response = json.dumps(data, indent=2, ensure_ascii=False)

        self.send_response(status_code)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Content-Length', str(len(response)))
        self.end_headers()
        self.wfile.write(response.encode('utf-8'))

    def _send_error(self, status_code: int, message: str):
        """发送错误响应"""
        self._send_json_response(status_code, {"error": message})

    def _handle_health(self):
        """处理健康检查请求"""
        try:
            metrics = get_metrics_collector()

            health_status = {
                "status": "healthy",
                "timestamp": time.time(),
                "version": "1.0.0",
                "metrics": {
                    "devices_connected": metrics._gauges.get("devices_connected", 0),
                    "queue_size": metrics._gauges.get("data_queue_size", 0),
                    "errors_total": metrics._counters.get("collector_errors", 0),
                    "data_points_processed": metrics._counters.get("data_points_processed", 0),
                }
            }

            self._send_json_response(200, health_status)

        except Exception as e:
            logger.error(f"Health check error: {e}")
            self._send_json_response(503, {
                "status": "unhealthy",
                "error": str(e)
            })

    def _handle_metrics(self, query: Dict[str, str]):
        """处理指标查询"""
        try:
            metrics = get_metrics_collector()
            format_type = query.get('format', ['json'])[0]

            if format_type == 'prometheus':
                # 返回Prometheus格式
                content = metrics.get_prometheus_metrics()
                if content:
                    self.send_response(200)
                    self.send_header('Content-Type', 'text/plain; version=0.0.4')
                    self.end_headers()
                    self.wfile.write(content.encode('utf-8'))
                else:
                    self._send_error(501, "Prometheus metrics not available")
            else:
                # 返回JSON格式
                metrics_data = metrics.export_to_dict()

                # 支持过滤
                if 'name' in query:
                    name = query['name'][0]
                    filtered_metrics = {
                        "counters": {k: v for k, v in metrics_data["counters"].items() if name in k},
                        "gauges": {k: v for k, v in metrics_data["gauges"].items() if name in k},
                        "histograms": {k: v for k, v in metrics_data["histograms"].items() if name in k},
                    }
                    metrics_data = filtered_metrics

                self._send_json_response(200, metrics_data)

        except Exception as e:
            logger.error(f"Metrics error: {e}")
            self._send_error(500, f"Failed to get metrics: {str(e)}")

    def _handle_devices(self, query: Dict[str, str]):
        """处理设备状态查询"""
        try:
            devices_list = []
            online_count = 0
            offline_count = 0

            if self.device_manager:
                for device in self.device_manager.get_all_devices():
                    is_online = device.status.value == "online"
                    if is_online:
                        online_count += 1
                    else:
                        offline_count += 1

                    devices_list.append({
                        "id": device.device_id,
                        "type": device.device_type,
                        "protocol": device.protocol,
                        "status": device.status.value,
                        "variables": len(device.variables),
                        "error_count": device.error_count,
                    })

            devices = {
                "devices": devices_list,
                "summary": {
                    "total": len(devices_list),
                    "online": online_count,
                    "offline": offline_count
                }
            }

            self._send_json_response(200, devices)

        except Exception as e:
            logger.error(f"Devices query error: {e}")
            self._send_error(500, f"Failed to get devices: {str(e)}")

    def _handle_config(self):
        """处理配置查询"""
        try:
            config_data = {
                "config": {},
                "last_updated": time.time()
            }

            if self.config:
                config_data["config"] = {
                    "tdengine": {
                        "host": self.config.tdengine.host,
                        "port": self.config.tdengine.port,
                        "database": self.config.tdengine.database,
                    },
                    "collector": {
                        "batch_size": self.config.batch_size,
                        "flush_interval": self.config.flush_interval,
                        "max_window_seconds": self.config.max_window_seconds
                    },
                    "queue": {
                        "max_size": self.config.queue_max_size
                    }
                }

            self._send_json_response(200, config_data)

        except Exception as e:
            logger.error(f"Config query error: {e}")
            self._send_error(500, f"Failed to get config: {str(e)}")

    def _handle_index(self):
        """处理根路径请求"""
        index = {
            "name": "Industrial Data Collector API",
            "version": "1.0.0",
            "endpoints": {
                "GET /health": "Health check",
                "GET /metrics": "Performance metrics",
                "GET /devices": "Device status",
                "GET /config": "Configuration",
                "GET /metrics?format=prometheus": "Prometheus metrics"
            },
            "status": "running"
        }

        self._send_json_response(200, index)

    def log_message(self, format, *args):
        """自定义日志格式"""
        logger.info(f"API {self.address_string()} - {format % args}")


class MonitorAPIServer:
    """监控API服务器"""

    def __init__(
        self,
        host: str = "0.0.0.0",
        port: int = 8080,
        device_manager: Optional["DeviceManager"] = None,
        config: Optional["CollectorConfig"] = None,
    ):
        self.host = host
        self.port = port
        self.device_manager = device_manager
        self.config = config
        # 设置类变量，让 Handler 可以访问
        MonitorAPIHandler.device_manager = device_manager
        MonitorAPIHandler.config = config
        self.server = HTTPServer((host, port), MonitorAPIHandler)
        self.server.timeout = 30  # 30秒超时
        self._running = False

    async def start(self):
        """启动API服务器"""
        if self._running:
            return

        self._running = True

        # 在单独的线程中运行HTTP服务器
        loop = asyncio.get_event_loop()
        await loop.run_in_executor(None, self._run_server)

        logger.info(f"Monitor API server started on http://{self.host}:{self.port}")

    async def stop(self):
        """停止API服务器"""
        if not self._running:
            return

        self._running = False
        self.server.shutdown()
        logger.info("Monitor API server stopped")

    def _run_server(self):
        """运行HTTP服务器（在单独线程中）"""
        logger.info(f"Starting HTTP server on {self.host}:{self.port}")
        try:
            self.server.serve_forever()
        except Exception as e:
            logger.error(f"HTTP server error: {e}")
        finally:
            logger.info("HTTP server stopped")


# 全局API服务器实例
_api_server = None


def get_api_server() -> MonitorAPIServer:
    """获取API服务器实例"""
    global _api_server
    if _api_server is None:
        _api_server = MonitorAPIServer()
    return _api_server


def init_api_server(
    host: str = "0.0.0.0",
    port: int = 8080,
    device_manager: Optional["DeviceManager"] = None,
    config: Optional["CollectorConfig"] = None,
) -> MonitorAPIServer:
    """初始化API服务器"""
    global _api_server
    _api_server = MonitorAPIServer(host, port, device_manager, config)
    return _api_server