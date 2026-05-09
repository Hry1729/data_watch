"""日志管理模块"""

import logging
import os
import sys
from logging.handlers import RotatingFileHandler
from typing import Optional


def setup_logger(
    name: str = "collector",
    level: str = "INFO",
    log_file: Optional[str] = None,
    log_max_bytes: int = 10 * 1024 * 1024,
    log_backup_count: int = 5,
) -> logging.Logger:
    """创建并配置日志记录器

    Args:
        name: 日志记录器名称
        level: 日志级别
        log_file: 日志文件路径，为空则只输出控制台
        log_max_bytes: 单个日志文件最大字节数
        log_backup_count: 保留的历史日志文件数量
    """
    logger = logging.getLogger(name)
    logger.setLevel(getattr(logging, level.upper(), logging.INFO))

    if not logger.handlers:
        formatter = logging.Formatter(
            "%(asctime)s [%(levelname)s] %(name)s - %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S",
        )

        # 控制台输出
        console_handler = logging.StreamHandler(sys.stdout)
        console_handler.setFormatter(formatter)
        logger.addHandler(console_handler)

        # 文件输出
        if log_file:
            # 确保日志目录存在
            log_dir = os.path.dirname(log_file)
            if log_dir and not os.path.exists(log_dir):
                os.makedirs(log_dir, exist_ok=True)

            file_handler = RotatingFileHandler(
                log_file,
                maxBytes=log_max_bytes,
                backupCount=log_backup_count,
                encoding="utf-8",
            )
            file_handler.setFormatter(formatter)
            logger.addHandler(file_handler)

    return logger


def get_logger(name: str) -> logging.Logger:
    """获取已有日志记录器"""
    return logging.getLogger(f"collector.{name}")
