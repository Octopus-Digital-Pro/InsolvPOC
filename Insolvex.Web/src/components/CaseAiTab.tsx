import { useState, useEffect, useRef } from "react";
import { useTranslation } from "@/contexts/LanguageContext";
import { caseAiApi } from "@/services/api/caseAiApi";
import type {
  AiChatMessageDto,
  AiEnabledStatus,
  AiSummaryDto,
} from "@/services/api/caseAiApi";
import { Button } from "@/components/ui/button";
import {
  Brain,
  RefreshCw,
  Loader2,
  Send,
  Trash2,
  Bot,
  User,
  AlertCircle,
  Sparkles,
  BarChart3,
} from "lucide-react";
import { format } from "date-fns";

interface Props {
  caseId: string;
  readOnly?: boolean;
}

type Lang = "en" | "ro" | "hu";

/** Extract the text for the requested language from textByLanguageJson, falling back to the default text. */
function tryGetLocalised(dto: AiSummaryDto, lang: Lang): string {
  if (dto.textByLanguageJson) {
    try {
      const map = JSON.parse(dto.textByLanguageJson) as Record<string, string>;
      if (map[lang]) return map[lang];
    } catch { /* ignore */ }
  }
  return dto.text;
}

export default function CaseAiTab({ caseId, readOnly = false }: Props) {
  const { t } = useTranslation();

  const [aiStatus, setAiStatus] = useState<AiEnabledStatus | null>(null);
  const [statusLoading, setStatusLoading] = useState(true);

  const [summary, setSummary] = useState<string | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryGeneratedAt, setSummaryGeneratedAt] = useState<string | null>(
    null
  );
  const [savedSummary, setSavedSummary] = useState<AiSummaryDto | null>(null);

  const [lang, setLang] = useState<Lang>("en");

  const [messages, setMessages] = useState<AiChatMessageDto[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [chatInput, setChatInput] = useState("");
  const [chatSending, setChatSending] = useState(false);
  const [chatError, setChatError] = useState<string | null>(null);
  const [clearConfirm, setClearConfirm] = useState(false);

  const messagesEndRef = useRef<HTMLDivElement>(null);

  // ── Load AI status on mount ────────────────────────────────────────────────
  useEffect(() => {
    setStatusLoading(true);
    caseAiApi
      .checkEnabled(caseId)
      .then(r => setAiStatus(r.data))
      .catch(console.error)
      .finally(() => setStatusLoading(false));
  }, [caseId]);

  // ── Load chat history once AI status known ─────────────────────────────────
  useEffect(() => {
    if (!aiStatus?.aiEnabled) return;
    setHistoryLoading(true);
    caseAiApi
      .getChatHistory(caseId)
      .then(r => setMessages(r.data))
      .catch(console.error)
      .finally(() => setHistoryLoading(false));
  }, [caseId, aiStatus?.aiEnabled]);
  // ── Load saved summary once AI status known ──────────────────────────────
  useEffect(() => {
    if (!aiStatus?.summaryEnabled) return;
    caseAiApi
      .getSummary(caseId)
      .then(r => {
        if (!r.data) return;
        setSavedSummary(r.data);
        // Pick the localised text if available, else fall back to default text
        const localised = tryGetLocalised(r.data, lang);
        setSummary(localised);
        setSummaryGeneratedAt(r.data.generatedAt);
      })
      .catch(console.error);
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [caseId, aiStatus?.summaryEnabled]);
  // ── When lang changes, swap localised text from saved summary if available ──
  useEffect(() => {
    if (!savedSummary) return;
    setSummary(tryGetLocalised(savedSummary, lang));
  }, [lang, savedSummary]);

  // ── Scroll chat to bottom on new messages ─────────────────────────────────
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // ── Generate summary ───────────────────────────────────────────────────────
  async function handleGenerateSummary() {
    setSummaryLoading(true);
    try {
      const res = await caseAiApi.generateSummary(caseId, lang);
      setSavedSummary(res.data);
      setSummary(tryGetLocalised(res.data, lang));
      setSummaryGeneratedAt(res.data.generatedAt ?? null);
      // Refresh token usage
      const status = await caseAiApi.checkEnabled(caseId);
      setAiStatus(status.data);
    } catch {
      // fail silently — keep whatever summary exists
    } finally {
      setSummaryLoading(false);
    }
  }

  // ── Send chat message ──────────────────────────────────────────────────────
  async function handleSend() {
    const text = chatInput.trim();
    if (!text || chatSending) return;
    setChatInput("");
    setChatError(null);

    // Optimistically add user bubble
    const tempUserMsg: AiChatMessageDto = {
      id: `temp-${Date.now()}`,
      role: "user",
      content: text,
      createdAt: new Date().toISOString(),
      tokensUsed: 0,
      model: null,
      userId: null,
      userName: null,
    };
    setMessages((prev) => [...prev, tempUserMsg]);
    setChatSending(true);

    try {
      const res = await caseAiApi.sendMessage(caseId, {
        message: text,
        language: lang,
      });

      // Replace temp user with confirmed + add assistant reply
      setMessages((prev) =>
        prev
          .filter((m) => m.id !== tempUserMsg.id)
          .concat([res.data.userMessage, res.data.assistantMessage])
      );

      // Refresh token usage
      const status = await caseAiApi.checkEnabled(caseId);
      setAiStatus(status.data);
    } catch (err: unknown) {
      setMessages((prev) => prev.filter((m) => m.id !== tempUserMsg.id));
      const message =
        err instanceof Error && err.message.includes("429")
          ? t.ai.chatAtLimit
          : t.ai.chatError;
      setChatError(message);
    } finally {
      setChatSending(false);
    }
  }

  // ── Clear history ──────────────────────────────────────────────────────────
  async function handleClear() {
    setClearConfirm(false);
    await caseAiApi.clearHistory(caseId);
    setMessages([]);
  }

  // ── Loading skeleton ───────────────────────────────────────────────────────
  if (statusLoading) {
    return (
      <div className="flex items-center justify-center h-40 text-muted-foreground gap-2">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span className="text-sm">{t.common.loading}</span>
      </div>
    );
  }

  // ── AI disabled banner ─────────────────────────────────────────────────────
  if (!aiStatus?.aiEnabled) {
    return (
      <div className="flex flex-col items-center justify-center h-56 gap-4 text-center p-6">
        <AlertCircle className="h-10 w-10 text-muted-foreground" />
        <p className="text-base font-semibold text-foreground">{t.ai.aiDisabled}</p>
        <p className="text-sm text-muted-foreground max-w-sm">{t.ai.aiDisabledDesc}</p>
      </div>
    );
  }

  const usagePct = aiStatus?.usagePercent ?? 0;

  return (
    <div className="flex flex-col gap-6">
      {/* ── Token usage bar ─────────────────────────────────────────────── */}
      {aiStatus && usagePct > 0 && (
        <div className="flex items-center gap-3 rounded-lg border border-border bg-card px-4 py-3">
          <BarChart3 className="h-4 w-4 text-muted-foreground shrink-0" />
          <div className="flex-1 min-w-0">
            <p className="text-xs text-muted-foreground mb-1">{t.ai.tokenUsage}</p>
            <div className="h-1.5 w-full rounded-full bg-muted overflow-hidden">
              <div
                className={`h-full rounded-full transition-all ${
                  usagePct >= 90
                    ? "bg-destructive"
                    : usagePct >= 70
                    ? "bg-yellow-500"
                    : "bg-primary"
                }`}
                style={{ width: `${usagePct}%` }}
              />
            </div>
          </div>
          <span className="text-xs text-muted-foreground shrink-0">
            {usagePct}%{aiStatus.atLimit && " ⚠"}
          </span>
        </div>
      )}

      {/* ── Language selector ────────────────────────────────────────────── */}
      <div className="flex items-center gap-2">
        <span className="text-xs font-medium text-muted-foreground">{t.ai.language}:</span>
        {(["en", "ro", "hu"] as Lang[]).map((l) => (
          <button
            key={l}
            onClick={() => setLang(l)}
            className={`px-3 py-1 rounded-full text-xs font-medium transition-colors ${
              lang === l
                ? "bg-primary text-primary-foreground"
                : "bg-muted text-muted-foreground hover:bg-accent hover:text-foreground"
            }`}
          >
            {l === "en" ? t.ai.langEn : l === "ro" ? t.ai.langRo : t.ai.langHu}
          </button>
        ))}
      </div>

      {/* ── AI Summary section ───────────────────────────────────────────── */}
      {aiStatus.summaryEnabled && (
        <section className="rounded-xl border border-border bg-card overflow-hidden">
          <div className="flex items-center justify-between px-5 py-4 border-b border-border">
            <div className="flex items-center gap-2">
              <Brain className="h-5 w-5 text-primary" />
              <div>
                <p className="text-sm font-semibold text-foreground">{t.ai.summaryTitle}</p>
                <p className="text-xs text-muted-foreground">{t.ai.summarySubtitle}</p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              {summaryGeneratedAt && (
                <span className="text-[10px] text-muted-foreground">
                  {t.ai.generatedAt}:{" "}
                  {format(new Date(summaryGeneratedAt), "dd MMM yyyy HH:mm")}
                </span>
              )}
              <Button
                size="sm"
                variant={summary ? "outline" : "default"}
                onClick={handleGenerateSummary}
                disabled={summaryLoading || readOnly}
              >
                {summaryLoading ? (
                  <>
                    <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                    {t.ai.generating}
                  </>
                ) : summary ? (
                  <>
                    <RefreshCw className="h-3.5 w-3.5 mr-1.5" />
                    {t.ai.refreshSummary}
                  </>
                ) : (
                  <>
                    <Sparkles className="h-3.5 w-3.5 mr-1.5" />
                    {t.ai.generateSummary}
                  </>
                )}
              </Button>
            </div>
          </div>

          <div className="px-5 py-4">
            {summary ? (
              <SummaryMarkdown content={summary} />
            ) : (
              <p className="text-sm text-muted-foreground italic">{t.ai.noSummary}</p>
            )}
          </div>
        </section>
      )}

      {/* ── Chat section ─────────────────────────────────────────────────── */}
      {aiStatus.chatEnabled && (
        <section className="rounded-xl border border-border bg-card overflow-hidden flex flex-col">
          {/* Header */}
          <div className="flex items-center justify-between px-5 py-4 border-b border-border">
            <div className="flex items-center gap-2">
              <Bot className="h-5 w-5 text-primary" />
              <p className="text-sm font-semibold text-foreground">{t.ai.chatTitle}</p>
            </div>
            {messages.length > 0 && !readOnly && (
              <div>
                {clearConfirm ? (
                  <div className="flex items-center gap-2">
                    <span className="text-xs text-muted-foreground">{t.ai.chatClearConfirm}</span>
                    <Button size="sm" variant="destructive" onClick={handleClear}>
                      {t.common.yes}
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      onClick={() => setClearConfirm(false)}
                    >
                      {t.common.no}
                    </Button>
                  </div>
                ) : (
                  <Button
                    size="sm"
                    variant="ghost"
                    onClick={() => setClearConfirm(true)}
                  >
                    <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                    {t.ai.chatClear}
                  </Button>
                )}
              </div>
            )}
          </div>

          {/* Messages */}
          <div className="flex-1 overflow-y-auto max-h-[400px] px-5 py-4 space-y-4">
            {historyLoading ? (
              <div className="flex items-center justify-center h-20 gap-2 text-muted-foreground">
                <Loader2 className="h-4 w-4 animate-spin" />
                <span className="text-sm">{t.common.loading}</span>
              </div>
            ) : messages.length === 0 ? (
              <p className="text-sm text-muted-foreground italic text-center py-6">
                {t.ai.chatEmpty}
              </p>
            ) : (
              messages.map((msg) => (
                <ChatBubble key={msg.id} message={msg} />
              ))
            )}
            {chatError && (
              <div className="flex items-center gap-2 text-destructive text-sm rounded-lg border border-destructive/30 bg-destructive/5 px-3 py-2">
                <AlertCircle className="h-4 w-4 shrink-0" />
                {chatError}
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          {/* Input */}
          {!readOnly && (
            <div className="border-t border-border px-4 py-3 flex items-end gap-2">
              <textarea
                value={chatInput}
                onChange={(e) => setChatInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    handleSend();
                  }
                }}
                placeholder={t.ai.chatPlaceholder}
                rows={2}
                className="flex-1 resize-none rounded-lg border border-input bg-background px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
                disabled={chatSending}
              />
              <Button
                size="sm"
                onClick={handleSend}
                disabled={chatSending || !chatInput.trim()}
              >
                {chatSending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}
              </Button>
            </div>
          )}
        </section>
      )}
    </div>
  );
}

// ── Sub-components ─────────────────────────────────────────────────────────────

function ChatBubble({ message }: { message: AiChatMessageDto }) {
  const isUser = message.role === "user";
  return (
    <div className={`flex gap-3 ${isUser ? "flex-row-reverse" : ""}`}>
      <div
        className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-full ${
          isUser ? "bg-primary text-primary-foreground" : "bg-muted text-muted-foreground"
        }`}
      >
        {isUser ? <User className="h-3.5 w-3.5" /> : <Bot className="h-3.5 w-3.5" />}
      </div>
      <div
        className={`max-w-[80%] rounded-2xl px-4 py-2.5 text-sm ${
          isUser
            ? "bg-primary text-primary-foreground rounded-tr-sm"
            : "bg-muted text-foreground rounded-tl-sm"
        }`}
      >
        <SummaryMarkdown content={message.content} dimColor={isUser} />
        <p
          className={`mt-1 text-[10px] ${
            isUser ? "text-primary-foreground/60 text-right" : "text-muted-foreground"
          }`}
        >
          {format(new Date(message.createdAt), "HH:mm")}
          {message.tokensUsed ? ` · ${message.tokensUsed} tok` : ""}
        </p>
      </div>
    </div>
  );
}

/** Very lightweight markdown-to-HTML renderer (no extra deps) */
function SummaryMarkdown({
  content,
  dimColor = false,
}: {
  content: string;
  dimColor?: boolean;
}) {
  // Convert markdown to HTML inline-styled paragraphs
  const html = content
    // Bold
    .replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
    // Italic
    .replace(/\*(.*?)\*/g, "<em>$1</em>")
    // Headings h3
    .replace(/^### (.+)$/gm, '<p class="font-semibold text-sm mt-3 mb-1">$1</p>')
    // Headings h2
    .replace(/^## (.+)$/gm, '<p class="font-bold text-sm mt-4 mb-1">$1</p>')
    // Bullet points
    .replace(/^[-*] (.+)$/gm, '<li class="ml-4 list-disc text-sm">$1</li>')
    // Numbered list
    .replace(/^\d+\. (.+)$/gm, '<li class="ml-4 list-decimal text-sm">$1</li>')
    // Horizontal rule
    .replace(/^---$/gm, '<hr class="my-3 border-border" />')
    // Paragraphs (lines not already wrapped)
    .split("\n")
    .map((line) => {
      if (
        line.startsWith("<p ") ||
        line.startsWith("<li") ||
        line.startsWith("<hr") ||
        line.trim() === ""
      )
        return line;
      return `<p class="text-sm mb-1">${line}</p>`;
    })
    .join("\n");

  return (
    <div
      className={`prose-sm max-w-none leading-relaxed ${dimColor ? "opacity-95" : ""}`}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
