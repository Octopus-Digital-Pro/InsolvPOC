import type {User} from "../types";
import logo from "../assets/logo.png";

interface HeaderProps {
  user: User;
  onLogout: () => void;
}

export default function Header({user, onLogout}: HeaderProps) {
  return (
    <header className="flex items-center justify-between border-b border-gray-200 bg-white px-6 py-3">
      <div className="flex items-center gap-3">
        <img
          src={logo}
          alt="InsolvPOC"
          className="h-6 w-auto  object-contain"
        />
        <h1 className="text-lg font-semibold text-gray-900">InsolvPOC</h1>
      </div>

      <div className="flex items-center gap-3">
        <div className="flex items-center gap-2">
          <img
            src={user.avatar}
            alt={user.name}
            className="h-7 w-7 rounded-full object-cover"
          />
          <span className="text-sm font-medium text-gray-700">{user.name}</span>
        </div>
        <button
          onClick={onLogout}
          className="rounded-md border border-gray-200 px-3 py-1.5 text-xs font-medium text-gray-500 transition-colors hover:border-gray-300 hover:bg-gray-50 hover:text-gray-700"
        >
          Switch user
        </button>
      </div>
    </header>
  );
}
