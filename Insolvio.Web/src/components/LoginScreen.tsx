import type {User} from "../types";
import {USERS} from "../types";
import logo from "../assets/logo.png";
import {ChevronRight} from "lucide-react";

interface LoginScreenProps {
  onLogin: (user: User) => void;
}

export default function LoginScreen({onLogin}: LoginScreenProps) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/30">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <img
            src={logo}
            alt="InsolvPOC"
            className="mx-auto mb-4 h-14 w-auto rounded-2xl object-contain shadow-lg"
          />
          <h1 className="text-2xl font-bold text-foreground">
            Your Insolvency Case Management System
          </h1>
        </div>

        <div className="space-y-3">
          <p className="mt-1 text-sm text-muted-foreground">
            Select your account to continue
          </p>
          {USERS.map((user) => (
            <button
              key={user.id}
              onClick={() => onLogin(user)}
              className="flex w-full items-center gap-4 cursor-pointer rounded-xl border border-border bg-card px-5 py-4 text-left transition-all hover:border-primary/30 hover:bg-accent hover:shadow-sm active:scale-[0.99]"
            >
              <img
                src={user.avatar}
                alt={user.name}
                className="h-11 w-11 shrink-0 rounded-full object-cover"
              />
              <div>
                <p className="text-sm font-semibold text-foreground">
                  {user.name}
                </p>
                <p className="text-xs text-muted-foreground">{user.role}</p>
              </div>
              <ChevronRight className="ml-auto h-5 w-5 text-muted-foreground" />
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
