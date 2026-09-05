from fastapi import Header, Query, HTTPException, status
from typing import Optional
from configs.settings import PIN_CODE

def verify_pin(x_pin_code: str = Header(...)):
    if x_pin_code != PIN_CODE:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Code PIN invalide"
        )

def verify_pin_query_or_header(
    x_pin_code: Optional[str] = Header(None),
    pin: Optional[str] = Query(None)
):
    # Accepte le PIN soit via le Header, soit via l'URL (?pin=...)
    provided_pin = x_pin_code or pin
    if provided_pin != PIN_CODE:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Code PIN invalide"
        )