import type {User} from "../types";
import logo from "../assets/logo.png";
import {Button} from "@/components/ui/button";

interface HeaderProps {
  user: User;
  onLogout: () => void;
}

export default function Header({user, onLogout}: HeaderProps) {
  return (
    <header className="flex items-center justify-between border-b border-border bg-background px-6 py-3">
      <div className="flex items-center gap-3">
        <img
          src={logo}
          alt="InsolvPOC"
          className="h-6 w-auto object-contain"
        />
        <h1 className="text-lg font-semibold text-foreground">InsolvPOC</h1>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2">
          <img
            src={user.avatar}
            alt={user.name}
            className="h-7 w-7 rounded-full object-cover"
          />
          <span className="text-sm font-medium text-foreground">{user.name}</span>
        </div>
        <Button variant="outline" size="sm" onClick={onLogout}>
          Switch user
        </Button>
      </div>
    </header>
  );
}
