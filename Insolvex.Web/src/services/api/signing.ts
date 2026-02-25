import client from "./client";

export interface WindowsCertInfo {
  thumbprint: string;
  subject: string;
  issuer: string;
  validFrom: string;
  validTo: string;
  serialNumber: string;
  friendlyName: string;
  storeLocation: string;
  keyAlgorithm: string;
  subjectKeyId: string;
}

export const signingApi = {
  // Key management
  getKeyStatus: () => client.get("/signing/keys/status"),
  getMyKeys: () => client.get("/signing/keys"),
  uploadKey: (file: File, password: string, name?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("password", password);
  if (name) formData.append("name", name);
    return client.post("/signing/keys/upload", formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  deactivateKey: (id: string) => client.delete(`/signing/keys/${id}`),

  // Signing
  signDocument: (documentId: string, pfxPassword: string, reason?: string) =>
    client.post(`/signing/sign/${documentId}`, { pfxPassword, reason }),
  downloadForSigning: (documentId: string) =>
    client.get(`/signing/download/${documentId}`, { responseType: "blob" }),
  uploadSigned: (documentId: string, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return client.post(`/signing/upload-signed/${documentId}`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Verification
  verifyDocument: (documentId: string) =>
    client.get(`/signing/verify/${documentId}`),
  checkSubmission: (documentId: string) =>
    client.get(`/signing/check-submission/${documentId}`),
  getMySignatures: () => client.get("/signing/my-signatures"),

  // DigiSign / hardware token (Windows cert store)
  getWindowsCerts: () => client.get<{ available: boolean; reason?: string; certificates: WindowsCertInfo[] }>("/signing/keys/windows-certs"),
};
