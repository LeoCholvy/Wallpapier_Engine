from fastapi import APIRouter, Depends, UploadFile, File, Form, HTTPException
from fastapi.responses import FileResponse
from sqlalchemy.orm import Session
from datetime import datetime
from typing import Optional

from models.database import get_db
from security.auth import verify_pin
from services.photo_service import save_photo, get_photo_path, update_favorite
from dto.photo_dto import FavoriteUpdateDto, PhotoMetadataDto

router = APIRouter(prefix="/api/photos", tags=["Photos"], dependencies=[Depends(verify_pin)])


@router.post("", response_model=PhotoMetadataDto)
def upload_photo(
        file: UploadFile = File(...),
        location: Optional[str] = Form(None),
        capture_date: Optional[datetime] = Form(None),
        db: Session = Depends(get_db)
):
    if file.content_type not in ["image/jpeg", "image/jpg"]:
        raise HTTPException(400, "Seul le format JPEG est accepté.")

    photo = save_photo(db, file, location, capture_date)
    return photo


@router.get("/{photo_id}")
def download_photo(photo_id: str):
    path = get_photo_path(photo_id)
    if not path.exists():
        raise HTTPException(status_code=404, detail="Image introuvable")
    return FileResponse(path, media_type="image/jpeg")


@router.patch("/{photo_id}/favorite")
def change_favorite_status(photo_id: str, payload: FavoriteUpdateDto, db: Session = Depends(get_db)):
    photo = update_favorite(db, photo_id, payload.is_favorite)
    if not photo:
        raise HTTPException(status_code=404, detail="Photo introuvable")
    return {"status": "ok", "is_favorite": photo.is_favorite}