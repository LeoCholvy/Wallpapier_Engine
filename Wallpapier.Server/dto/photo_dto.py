from pydantic import BaseModel
from typing import Optional
from datetime import datetime

class PhotoMetadataDto(BaseModel):
    id: str
    filename: str
    is_favorite: bool
    capture_date: Optional[datetime] = None
    location: Optional[str] = None
    upload_date: datetime

class FavoriteUpdateDto(BaseModel):
    is_favorite: bool