from __future__ import annotations

from sqlalchemy.ext.asyncio import AsyncSession

from app.models.collector import CollectorSetting

DEFAULT_STREAM_MAXLEN = 50000


class CollectorConfigService:
    """collector 运行参数配置读写（collector_setting 单行表，id=1）。"""

    def __init__(self, session: AsyncSession) -> None:
        self.session = session

    async def get_settings(self) -> dict:
        setting = await self.session.get(CollectorSetting, 1)
        if setting is None:
            return {"streamMaxlen": DEFAULT_STREAM_MAXLEN}
        return {"streamMaxlen": setting.stream_maxlen}

    async def update_stream_maxlen(self, value: int) -> dict:
        setting = await self.session.get(CollectorSetting, 1)
        if setting is None:
            setting = CollectorSetting(id=1, stream_maxlen=value)
            self.session.add(setting)
        else:
            setting.stream_maxlen = value
        await self.session.commit()
        return {"streamMaxlen": value}
