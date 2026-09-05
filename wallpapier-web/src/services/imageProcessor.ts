import heic2any from "heic2any";

const MAX_HD = 1920;
const MAX_QHD = 2560;
const THUMB_SIZE = 400;

export interface ProcessedImages {
    main: File;
    thumb: File;
}

export const processImage = async (file: File, maxRes: "HD" | "QHD"): Promise<ProcessedImages> => {
    let imageBlob: Blob = file;

    if (file.name.toLowerCase().endsWith(".heic") || file.name.toLowerCase().endsWith(".heif")) {
        const converted = await heic2any({ blob: file, toType: "image/jpeg", quality: 0.85 });
        imageBlob = Array.isArray(converted) ? converted[0] : converted;
    }

    const imgUrl = URL.createObjectURL(imageBlob);
    const img = new Image();
    await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = reject;
        img.src = imgUrl;
    });

    const generateCanvasBlob = (targetWidth: number, targetHeight: number, quality: number): Promise<Blob> => {
        const canvas = document.createElement("canvas");
        canvas.width = targetWidth;
        canvas.height = targetHeight;
        const ctx = canvas.getContext("2d");
        ctx?.drawImage(img, 0, 0, targetWidth, targetHeight);

        return new Promise((resolve, reject) => {
            canvas.toBlob((blob) => {
                if (blob) resolve(blob);
                else reject(new Error("Canvas to Blob failed"));
            }, "image/jpeg", quality);
        });
    };

    // 1. Calcul pour l'image principale
    const maxDim = maxRes === "QHD" ? MAX_QHD : MAX_HD;
    let mainW = img.width;
    let mainH = img.height;
    if (mainW > maxDim || mainH > maxDim) {
        if (mainW > mainH) { mainH = Math.round((mainH * maxDim) / mainW); mainW = maxDim; }
        else { mainW = Math.round((mainW * maxDim) / mainH); mainH = maxDim; }
    }

    // 2. Calcul pour la miniature
    let thumbW = img.width;
    let thumbH = img.height;
    if (thumbW > THUMB_SIZE || thumbH > THUMB_SIZE) {
        if (thumbW > thumbH) { thumbH = Math.round((thumbH * THUMB_SIZE) / thumbW); thumbW = THUMB_SIZE; }
        else { thumbW = Math.round((thumbW * THUMB_SIZE) / thumbH); thumbH = THUMB_SIZE; }
    }

    // Génération parallèle des deux blobs pour plus de rapidité
    const [mainBlob, thumbBlob] = await Promise.all([
        generateCanvasBlob(mainW, mainH, 0.85),
        generateCanvasBlob(thumbW, thumbH, 0.70)
    ]);

    URL.revokeObjectURL(imgUrl);
    const baseName = file.name.replace(/\.[^/.]+$/, "");

    return {
        main: new File([mainBlob], `${baseName}.jpg`, { type: "image/jpeg" }),
        thumb: new File([thumbBlob], `${baseName}_thumb.jpg`, { type: "image/jpeg" })
    };
};