from sqlalchemy import Column, String, Boolean, DateTime
from models.database import Base

class ServerPhoto(Base):
    __tablename__ = "Server_Photos"

    id = Column(String(36), primary_key=True, index=True)
    filename = Column(String, nullable=False)
    is_favorite = Column(Boolean, default=False, nullable=False)
    capture_date = Column(DateTime, nullable=True)
    location = Column(String, nullable=True)
    upload_date = Column(DateTime, nullable=False)

class ServerSystemState(Base):
    __tablename__ = "Server_SystemState"

    key = Column(String, primary_key=True, index=True)
    value = Column(String, nullable=False)