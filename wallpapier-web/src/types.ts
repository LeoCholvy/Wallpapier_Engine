export interface AppSettings {
    serverIp: string;
    pinCode: string;
    maxResolution: "HD" | "QHD";
}

export interface PhotoMetadata {
    id: string;
    filename: string;
    is_favorite: boolean;
    capture_date?: string;
    location?: string;
    upload_date: string;
}

export type UploadStatus = "pending" | "converting" | "uploading" | "done" | "error";

export interface UploadItem {
    id: string; // ID temporaire
    file: File;
    status: UploadStatus;
    progress: number;
    error?: string;
}