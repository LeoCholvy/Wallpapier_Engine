import asyncio
from fastapi import APIRouter, Depends, UploadFile, File, Form, HTTPException
from fastapi.responses import FileResponse
from sqlalchemy.orm import Session
from datetime import datetime
from typing import Optional
from starlette.concurrency import run_in_threadpool

from models.database import get_db
from security.auth import verify_pin, verify_pin_query_or_header
from services.photo_service import save_photo, get_photo_path, get_thumb_path, update_favorite
from dto.photo_dto import FavoriteUpdateDto, PhotoMetadataDto

router = APIRouter(prefix="/api/photos", tags=["Photos"])

UPLOAD_LOCK = asyncio.Lock()
DOWNLOAD_LOCK = asyncio.Lock()
LOCK_TIMEOUT = 120.0


@router.post("", response_model=PhotoMetadataDto, dependencies=[Depends(verify_pin)])
async def upload_photo(
        file: UploadFile = File(...),
        location: Optional[str] = Form(None),
        capture_date: Optional[datetime] = Form(None),
        db: Session = Depends(get_db)
):
    if file.content_type not in ["image/jpeg", "image/jpg"]:
        raise HTTPException(400, "Seul le format JPEG est accepté.")

    try:
        async with asyncio.timeout(LOCK_TIMEOUT):
            async with UPLOAD_LOCK:
                photo = await run_in_threadpool(save_photo, db, file, location, capture_date)
                return photo
    except TimeoutError:
        raise HTTPException(status_code=504, detail="Serveur occupé.")


# Nouvelle auth pour accepter ?pin=...
@router.get("/{photo_id}", dependencies=[Depends(verify_pin_query_or_header)])
async def download_photo(photo_id: str):
    path = get_photo_path(photo_id)
    if not path.exists():
        raise HTTPException(status_code=404, detail="Image introuvable")
    try:
        async with asyncio.timeout(LOCK_TIMEOUT):
            async with DOWNLOAD_LOCK:
                return FileResponse(path, media_type="image/jpeg")
    except TimeoutError:
        raise HTTPException(status_code=504, detail="Serveur occupé.")


# Nouvelle route pour la miniature
@router.get("/{photo_id}/thumb", dependencies=[Depends(verify_pin_query_or_header)])
def download_thumb(photo_id: str):
    path = get_thumb_path(photo_id)
    if not path.exists():
        # Fallback sur l'original si la miniature manque exceptionnellement
        path = get_photo_path(photo_id)
        if not path.exists():
            raise HTTPException(status_code=404, detail="Image introuvable")
    return FileResponse(path, media_type="image/jpeg")


@router.patch("/{photo_id}/favorite", dependencies=[Depends(verify_pin)])
def change_favorite_status(photo_id: str, payload: FavoriteUpdateDto, db: Session = Depends(get_db)):
    photo = update_favorite(db, photo_id, payload.is_favorite)
    if not photo:
        raise HTTPException(status_code=404, detail="Photo introuvable")
    return {"status": "ok", "is_favorite": photo.is_favorite}