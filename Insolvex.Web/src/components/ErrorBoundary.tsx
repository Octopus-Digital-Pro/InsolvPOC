import React from "react";
import { AlertTriangle, RefreshCw } from "lucide-react";
import { Button } from "@/components/ui/button";

interface ErrorBoundaryState {
  hasError: boolean;
  error: Error | null;
  errorInfo: React.ErrorInfo | null;
}

interface ErrorBoundaryProps {
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export class ErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false, error: null, errorInfo: null };
  }

  static getDerivedStateFromError(error: Error): Partial<ErrorBoundaryState> {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: React.ErrorInfo) {
    this.setState({ errorInfo });
    // Log to console in development
    console.error("[ErrorBoundary]", error, errorInfo);
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
  };

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      if (this.props.fallback) {
        return this.props.fallback;
    }

      return (
    <div className="flex min-h-screen items-center justify-center bg-background p-4">
    <div className="mx-auto max-w-md space-y-4 text-center">
            <AlertTriangle className="mx-auto h-12 w-12 text-destructive" />
      <h1 className="text-xl font-bold text-foreground">Something went wrong</h1>
            <p className="text-sm text-muted-foreground">
  An unexpected error occurred. You can try again or reload the page.
         </p>

     {this.state.error && (
              <pre className="mx-auto max-w-full overflow-x-auto rounded-lg bg-muted p-3 text-left text-xs font-mono text-foreground">
   {this.state.error.message}
     </pre>
 )}

        <div className="flex justify-center gap-3">
              <Button variant="outline" size="sm" onClick={this.handleReset}>
           Try Again
     </Button>
              <Button size="sm" className="gap-1.5" onClick={this.handleReload}>
  <RefreshCw className="h-3.5 w-3.5" />
  Reload Page
          </Button>
            </div>
    </div>
        </div>
      );
  }

    return this.props.children;
  }
}

export default ErrorBoundary;
