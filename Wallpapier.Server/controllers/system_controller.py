from fastapi import APIRouter, Depends
from security.auth import verify_pin
from dto.system_dto import ResetTimeUpdateDto
from services.reset_service import update_reset_time_in_db
from configs.settings import MAX_RESOLUTION_ALLOWED

router = APIRouter(prefix="/api/system", tags=["System"], dependencies=[Depends(verify_pin)])

@router.get("/status")
def get_status():
    return {"max_resolution": MAX_RESOLUTION_ALLOWED}

@router.put("/reset-time")
def update_reset_time(payload: ResetTimeUpdateDto):
    update_reset_time_in_db(payload.reset_time)
    return {"status": "ok", "new_reset_time": payload.reset_time}