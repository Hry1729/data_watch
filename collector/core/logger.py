"""日志管理模块"""

import logging
import sys


def setup_logger(name: str = "collector", level: str = "INFO") -> logging.Logger:
    """创建并配置日志记录器"""
    logger = logging.getLogger(name)
    logger.setLevel(getattr(logging, level.upper(), logging.INFO))

    if not logger.handlers:
        handler = logging.StreamHandler(sys.stdout)
        formatter = logging.Formatter(
            "%(asctime)s [%(levelname)s] %(name)s - %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
        )
        handler.setFormatter(formatter)
        logger.addHandler(handler)

    return logger


def get_logger(name: str) -> logging.Logger:
    """获取已有日志记录器"""
    return logging.getLogger(f"collector.{name}")
