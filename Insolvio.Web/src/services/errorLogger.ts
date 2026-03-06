import { settingsApi } from "@/services/api/settingsApi";

interface LogFrontendErrorInput {
  message: string;
  stackTrace?: string;
  source?: string;
  additionalContext?: string;
}

export async function logFrontendError(input: LogFrontendErrorInput): Promise<void> {
  try {
    await settingsApi.errors.logClient({
      message: input.message,
      stackTrace: input.stackTrace,
      source: input.source,
      requestPath: window.location.pathname,
      userAgent: navigator.userAgent,
      additionalContext: input.additionalContext,
    });
  } catch {
    // Ignore logging failures to avoid loops
  }
}
