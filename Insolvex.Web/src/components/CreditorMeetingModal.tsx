import { useState } from "react";
import { workflowApi } from "@/services/api/workflow";
import { Button } from "@/components/ui/button";
import {
    Loader2, Users, CalendarPlus, MapPin, FileText, X, Check,
} from "lucide-react";

interface Props {
    caseId: string;
open: boolean;
  onClose: () => void;
    onCreated?: () => void;
}

export default function CreditorMeetingModal({ caseId, open, onClose, onCreated }: Props) {
    const [meetingDate, setMeetingDate] = useState("");
    const [meetingTime, setMeetingTime] = useState("10:00");
    const [location, setLocation] = useState("");
    const [agenda, setAgenda] = useState("");
    const [duration, setDuration] = useState(2);
    const [creating, setCreating] = useState(false);
    const [result, setResult] = useState<Record<string, unknown> | null>(null);
    const [error, setError] = useState("");

  if (!open) return null;

const handleCreate = async () => {
 if (!meetingDate) return;
        setCreating(true);
        setError("");
        try {
   const dt = `${meetingDate}T${meetingTime}:00`;
            const r = await workflowApi.createMeeting({
         caseId,
  meetingDate: dt,
       location: location || undefined,
           agenda: agenda || undefined,
     durationHours: duration,
            });
            setResult(r.data);
          onCreated?.();
   } catch (e: unknown) {
            const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message || "Failed to create meeting";
            setError(msg);
     } finally {
          setCreating(false);
        }
    };

    const inputCls = "w-full rounded-md border border-input bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring";
  const labelCls = "mb-1 block text-xs font-semibold uppercase tracking-wide text-muted-foreground";

    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm" onClick={onClose}>
      <div
className="relative w-full max-w-lg rounded-xl border border-border bg-card shadow-xl p-6 mx-4"
        onClick={(e) => e.stopPropagation()}
            >
 <button onClick={onClose} className="absolute top-3 right-3 text-muted-foreground hover:text-foreground">
       <X className="h-4 w-4" />
     </button>

         <div className="flex items-center gap-2 mb-4">
           <Users className="h-5 w-5 text-primary" />
          <h2 className="text-base font-semibold text-foreground">Call Creditor Meeting</h2>
             </div>

          {result ? (
       <div className="space-y-3">
            <div className="flex items-center gap-2 p-3 rounded-lg bg-green-500/10 text-green-600 text-sm">
     <Check className="h-4 w-4" />
      {result.message as string}
            </div>
          <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground">
        <div>Tasks created: <span className="font-medium text-foreground">{result.taskCount as number}</span></div>
    <div>Notice deadline: <span className="font-medium text-foreground">{new Date(result.noticeSendDeadline as string).toLocaleDateString()}</span></div>
           </div>
       <Button size="sm" variant="outline" className="w-full mt-2" onClick={onClose}>Close</Button>
       </div>
             ) : (
       <div className="space-y-3">
                {error && (
            <div className="flex items-center gap-2 p-2 rounded-lg bg-destructive/10 text-destructive text-xs">
         {error}
        </div>
             )}

           <div className="grid gap-3 sm:grid-cols-2">
      <div>
       <label className={labelCls}><CalendarPlus className="inline h-3 w-3 mr-1" />Date</label>
            <input type="date" value={meetingDate} onChange={e => setMeetingDate(e.target.value)} className={inputCls} />
         </div>
      <div>
            <label className={labelCls}>Time</label>
             <input type="time" value={meetingTime} onChange={e => setMeetingTime(e.target.value)} className={inputCls} />
               </div>
            <div className="sm:col-span-2">
         <label className={labelCls}><MapPin className="inline h-3 w-3 mr-1" />Location / Link</label>
       <input value={location} onChange={e => setLocation(e.target.value)} className={inputCls} placeholder="Courtroom 3 / https://meet.google.com/..." />
  </div>
     <div>
         <label className={labelCls}>Duration (hours)</label>
        <input type="number" value={duration} onChange={e => setDuration(Number(e.target.value))} min={0.5} max={8} step={0.5} className={inputCls} />
  </div>
  <div className="sm:col-span-2">
      <label className={labelCls}><FileText className="inline h-3 w-3 mr-1" />Agenda</label>
      <textarea value={agenda} onChange={e => setAgenda(e.target.value)} className={inputCls + " min-h-[80px]"} placeholder="Meeting agenda items..." rows={3} />
        </div>
   </div>

<Button
       className="w-full bg-primary hover:bg-primary/90 gap-1"
            onClick={handleCreate}
         disabled={creating || !meetingDate}
             >
   {creating ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Users className="h-3.5 w-3.5" />}
    Schedule Meeting
   </Button>
           </div>
           )}
            </div>
      </div>
    );
}
