from fastapi import Header, HTTPException, status
from configs.settings import PIN_CODE

def verify_pin(x_pin_code: str = Header(...)):
    if x_pin_code != PIN_CODE:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Code PIN invalide"
        )