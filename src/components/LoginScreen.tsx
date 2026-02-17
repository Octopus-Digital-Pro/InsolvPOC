import type {User} from "../types";
import {USERS} from "../types";
import logo from "../assets/logo.png";

interface LoginScreenProps {
  onLogin: (user: User) => void;
}

export default function LoginScreen({onLogin}: LoginScreenProps) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gray-50">
      <div className="w-full max-w-sm">
        <div className="mb-8 text-center">
          <img src={logo} alt="InsolvPOC" className="mx-auto mb-4 h-14 w-14 rounded-2xl object-contain shadow-lg" />
          <h1 className="text-2xl font-bold text-gray-900">InsolvPOC</h1>
          <p className="mt-1 text-sm text-gray-500">
            Select your account to continue
          </p>
        </div>

        <div className="space-y-3">
          {USERS.map((user) => (
            <button
              key={user.id}
              onClick={() => onLogin(user)}
              className="flex w-full items-center gap-4 cursor-pointer rounded-xl border border-gray-200 bg-white px-5 py-4 text-left transition-all hover:border-blue-300 hover:bg-blue-50 hover:shadow-sm active:scale-[0.99]"
            >
              <img
                src={user.avatar}
                alt={user.name}
                className="h-11 w-11 shrink-0 rounded-full object-cover"
              />
              <div>
                <p className="text-sm font-semibold text-gray-800">
                  {user.name}
                </p>
                <p className="text-xs text-gray-400">Insolvency Practitioner</p>
              </div>
              <svg
                className="ml-auto h-5 w-5 text-gray-300"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M9 5l7 7-7 7"
                />
              </svg>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
