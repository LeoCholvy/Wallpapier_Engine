import axios from "axios";
import type { ProcessedImages } from "./imageProcessor";
import type { AppSettings, PhotoMetadata } from "../types"; // <-- Ajout de "type"

export const getBaseUrl = (ip: string) => {
    let url = ip;
    if (!url.startsWith("http")) url = `http://${url}`;
    return url.replace(/\/$/, ""); // Enlever le slash final
};

export const fetchServerStatus = async (settings: AppSettings) => {
    const url = `${getBaseUrl(settings.serverIp)}/api/system/status`;
    const res = await axios.get(url, { headers: { "X-PIN-Code": settings.pinCode } });
    return res.data;
};

export const fetchManifest = async (settings: AppSettings): Promise<PhotoMetadata[]> => {
    const url = `${getBaseUrl(settings.serverIp)}/api/manifest`;
    const res = await axios.get(url, { headers: { "X-PIN-Code": settings.pinCode } });
    return res.data.created;
};

export const toggleFavorite = async (settings: AppSettings, id: string, isFavorite: boolean) => {
    const url = `${getBaseUrl(settings.serverIp)}/api/photos/${id}/favorite`;
    await axios.patch(url, { is_favorite: isFavorite }, { headers: { "X-PIN-Code": settings.pinCode } });
};

export const uploadPhoto = async (
    settings: AppSettings,
    images: ProcessedImages,
    location: string,
    onProgress: (percent: number) => void
) => {
    const url = `${getBaseUrl(settings.serverIp)}/api/photos`;
    const formData = new FormData();

    // On envoie les deux fichiers sous les noms attendus par FastAPI
    formData.append("file", images.main, images.main.name);
    formData.append("thumb_file", images.thumb, images.thumb.name);

    if (location) formData.append("location", location);

    await axios.post(url, formData, {
        headers: { "X-PIN-Code": settings.pinCode, "Content-Type": "multipart/form-data" },
        onUploadProgress: (progressEvent) => {
            if (progressEvent.total) {
                const percent = Math.round((progressEvent.loaded * 100) / progressEvent.total);
                onProgress(percent);
            }
        }
    });
};