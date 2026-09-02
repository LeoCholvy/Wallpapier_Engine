from pydantic import BaseModel
from typing import List
from dto.photo_dto import PhotoMetadataDto

class FavoriteChangeDto(BaseModel):
    id: str
    is_favorite: bool

class ManifestResponseDto(BaseModel):
    created: List[PhotoMetadataDto]
    deleted: List[str]
    favorites_changed: List[FavoriteChangeDto]