from pydantic import BaseModel

class ResetTimeUpdateDto(BaseModel):
    reset_time: str