import heic2any from "heic2any";

const MAX_HD = 1920;
const MAX_QHD = 2560;

export const processImage = async (file: File, maxRes: "HD" | "QHD"): Promise<File> => {
    let imageBlob: Blob = file;

    // Conversion HEIC si nécessaire
    if (file.name.toLowerCase().endsWith(".heic") || file.name.toLowerCase().endsWith(".heif")) {
        const converted = await heic2any({ blob: file, toType: "image/jpeg", quality: 0.85 });
        imageBlob = Array.isArray(converted) ? converted[0] : converted;
    }

    // Redimensionnement via Canvas
    const maxDim = maxRes === "QHD" ? MAX_QHD : MAX_HD;
    const imgUrl = URL.createObjectURL(imageBlob);
    const img = new Image();

    await new Promise((resolve, reject) => {
        img.onload = resolve;
        img.onerror = reject;
        img.src = imgUrl;
    });

    let width = img.width;
    let height = img.height;

    // Calcul du ratio pour ne pas dépasser la taille max
    if (width > maxDim || height > maxDim) {
        if (width > height) {
            height = Math.round((height * maxDim) / width);
            width = maxDim;
        } else {
            width = Math.round((width * maxDim) / height);
            height = maxDim;
        }
    }

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    ctx?.drawImage(img, 0, 0, width, height);
    URL.revokeObjectURL(imgUrl);

    return new Promise((resolve, reject) => {
        canvas.toBlob((blob) => {
            if (blob) {
                resolve(new File([blob], file.name.replace(/\.[^/.]+$/, "") + ".jpg", { type: "image/jpeg" }));
            } else {
                reject(new Error("Canvas to Blob failed"));
            }
        }, "image/jpeg", 0.85); // 85% de qualité
    });
};