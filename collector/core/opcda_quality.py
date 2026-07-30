"""OPC DA 原始质量码到平台质量码的映射。"""

from core.quality_codes import (
    QUALITY_BAD,
    QUALITY_CONFIG_DECODE_ERROR,
    QUALITY_GOOD,
    QUALITY_UNCERTAIN,
)


def map_opcda_quality(opc_quality: int) -> int:
    """按 OPC DA 低字节的主质量位映射平台质量码。"""
    if type(opc_quality) is not int or not 0 <= opc_quality <= 0xFFFF:
        raise ValueError("OPC DA quality must be a 16-bit integer")

    primary_quality = opc_quality & 0xC0
    if primary_quality == 0xC0:
        return QUALITY_GOOD
    if primary_quality == 0x40:
        return QUALITY_UNCERTAIN
    if primary_quality == 0x00:
        return QUALITY_BAD
    return QUALITY_CONFIG_DECODE_ERROR
