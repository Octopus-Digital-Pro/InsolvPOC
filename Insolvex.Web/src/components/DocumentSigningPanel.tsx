import { useState, useEffect } from "react";
import { signingApi } from "@/services/api/signing";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
Loader2, Shield, ShieldCheck, ShieldAlert,
    Download, Upload, PenTool, CheckCircle2, XCircle,
} from "lucide-react";
import { format } from "date-fns";

interface Props {
    documentId: string;
  fileName: string;
    requiresSignature?: boolean;
    isSigned?: boolean;
}

interface SignatureInfo {
    id: string;
    signedAt: string;
    certificateSubject: string;
    reason: string;
    isValid: boolean;
}

export default function DocumentSigningPanel({ documentId, fileName, requiresSignature, isSigned }: Props) {
    const [hasKey, setHasKey] = useState(false);
const [canSign, setCanSign] = useState(false);
    const [signatures, setSignatures] = useState<SignatureInfo[]>([]);
    const [loading, setLoading] = useState(true);
  const [signing, setSigning] = useState(false);
    const [uploading, setUploading] = useState(false);
    const [password, setPassword] = useState("");
    const [reason, setReason] = useState("");
    const [showSignForm, setShowSignForm] = useState(false);
    const [message, setMessage] = useState({ type: "", text: "" });

    const load = async () => {
        setLoading(true);
        try {
            const [keyRes, verifyRes] = await Promise.all([
      signingApi.getKeyStatus(),
   signingApi.verifyDocument(documentId),
      ]);
    setHasKey(keyRes.data.hasKey);
            setCanSign(keyRes.data.canSign);
    setSignatures(verifyRes.data.signatures ?? []);
 } catch (e) {
    console.error(e);
   } finally {
  setLoading(false);
        }
    };

    useEffect(() => { load(); }, [documentId]);

  const handleSign = async () => {
        if (!password) return;
   setSigning(true);
    setMessage({ type: "", text: "" });
        try {
         await signingApi.signDocument(documentId, password, reason || undefined);
            setMessage({ type: "success", text: "Document signed successfully" });
            setShowSignForm(false);
  setPassword("");
   setReason("");
       await load();
        } catch (e: unknown) {
            const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || "Signing failed";
          setMessage({ type: "error", text: msg });
        } finally {
            setSigning(false);
        }
    };

    const handleDownload = async () => {
        try {
          const r = await signingApi.downloadForSigning(documentId);
            const url = window.URL.createObjectURL(new Blob([r.data]));
        const a = document.createElement("a");
            a.href = url;
         a.download = fileName;
          a.click();
      window.URL.revokeObjectURL(url);
     } catch (e) {
            console.error(e);
   }
    };

    const handleUploadSigned = async (file: File) => {
  setUploading(true);
        setMessage({ type: "", text: "" });
        try {
    await signingApi.uploadSigned(documentId, file);
   setMessage({ type: "success", text: "Signed document uploaded" });
        await load();
        } catch (e: unknown) {
            const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || "Upload failed";
        setMessage({ type: "error", text: msg });
        } finally {
    setUploading(false);
  }
    };

    if (loading) {
        return (
            <div className="flex items-center gap-2 py-2">
 <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />
       <span className="text-xs text-muted-foreground">Checking signature status...</span>
  </div>
        );
    }

    const signed = isSigned || signatures.length > 0;

    return (
   <div className="rounded-lg border border-border bg-card/50 p-3 space-y-2">
            {/* Status */}
         <div className="flex items-center justify-between">
          <div className="flex items-center gap-1.5">
       {signed ? (
            <ShieldCheck className="h-4 w-4 text-green-500" />
       ) : requiresSignature ? (
        <ShieldAlert className="h-4 w-4 text-amber-500" />
     ) : (
    <Shield className="h-4 w-4 text-muted-foreground" />
      )}
        <span className="text-xs font-medium">
         {signed ? "Signed" : requiresSignature ? "Signature Required" : "E-Signature"}
           </span>
    </div>
                {requiresSignature && !signed && (
           <Badge variant="destructive" className="text-[9px]">Required</Badge>
   )}
           {signed && (
   <Badge variant="success" className="text-[9px]">Verified</Badge>
 )}
            </div>

    {/* Message */}
 {message.text && (
        <div className={`flex items-center gap-1.5 p-1.5 rounded text-[10px] ${
      message.type === "success" ? "bg-green-500/10 text-green-600" : "bg-destructive/10 text-destructive"
         }`}>
    {message.type === "success" ? <CheckCircle2 className="h-3 w-3" /> : <XCircle className="h-3 w-3" />}
       {message.text}
   </div>
   )}

            {/* Signatures list */}
   {signatures.length > 0 && (
          <div className="space-y-1">
 {signatures.map((s) => (
        <div key={s.id} className="text-[10px] text-muted-foreground flex items-center gap-1">
        <CheckCircle2 className="h-2.5 w-2.5 text-green-500 shrink-0" />
             <span className="truncate">{s.certificateSubject}</span>
             <span className="shrink-0">· {format(new Date(s.signedAt), "dd MMM yyyy HH:mm")}</span>
              </div>
      ))}
      </div>
       )}

            {/* Actions */}
        <div className="flex flex-wrap gap-1.5">
  {/* Download for signing */}
       <Button variant="outline" size="sm" className="text-[10px] h-6 px-2 gap-1" onClick={handleDownload}>
     <Download className="h-2.5 w-2.5" />Download
       </Button>

           {/* Upload signed */}
    <label className="cursor-pointer">
    <input
   type="file"
       className="hidden"
     accept=".pdf,.p7s,.p7m"
    onChange={(e) => {
   const f = e.target.files?.[0];
  if (f) handleUploadSigned(f);
      }}
  />
           <Button variant="outline" size="sm" className="text-[10px] h-6 px-2 gap-1" asChild disabled={uploading}>
   <span>
    {uploading ? <Loader2 className="h-2.5 w-2.5 animate-spin" /> : <Upload className="h-2.5 w-2.5" />}
    Upload Signed
             </span>
    </Button>
    </label>

        {/* Sign with key */}
         {canSign && (
       <Button
      variant="outline"
  size="sm"
        className="text-[10px] h-6 px-2 gap-1 border-primary/30 text-primary"
onClick={() => setShowSignForm(!showSignForm)}
    >
         <PenTool className="h-2.5 w-2.5" />Sign with Key
           </Button>
      )}
            </div>

            {/* Sign form */}
{showSignForm && (
           <div className="rounded-lg border border-primary/20 bg-primary/5 p-2 space-y-2">
         <input
 type="password"
            value={password}
      onChange={(e) => setPassword(e.target.value)}
          className="w-full rounded-md border border-input bg-background px-2 py-1 text-xs"
     placeholder="Certificate password"
        />
    <input
  value={reason}
    onChange={(e) => setReason(e.target.value)}
         className="w-full rounded-md border border-input bg-background px-2 py-1 text-xs"
placeholder="Reason (optional)"
             />
               <Button
             size="sm"
   className="w-full text-xs h-7 bg-primary hover:bg-primary/90 gap-1"
        onClick={handleSign}
         disabled={signing || !password}
          >
       {signing ? <Loader2 className="h-3 w-3 animate-spin" /> : <PenTool className="h-3 w-3" />}
  Sign Document
        </Button>
        </div>
         )}

            {/* No key warning */}
         {!hasKey && !signed && requiresSignature && (
          <p className="text-[10px] text-amber-600">
 No signing key found. Go to Settings ? E-Signing to upload your PFX certificate.
              </p>
            )}
        </div>
    );
}
