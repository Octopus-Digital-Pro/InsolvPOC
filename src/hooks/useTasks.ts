import {useState, useCallback, useEffect} from "react";
import type {CompanyTask} from "../types";
import {storage} from "../services/storage";

export function useTasks(userId: string | null) {
  const [tasks, setTasks] = useState<CompanyTask[]>([]);
  const [loading, setLoading] = useState(true);

  const myTasks =
    userId == null ? [] : tasks.filter((t) => t.assignedTo === userId);

  const refresh = useCallback(async () => {
    const data = await storage.getTasks();
    setTasks(data);
  }, []);

  useEffect(() => {
    let cancelled = false;
    storage
      .getTasks()
      .then((data) => {
        if (!cancelled) {
          setTasks(data);
        }
      })
      .catch((err) => {
        console.error("Failed to load tasks from Firestore:", err);
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const getByCompany = useCallback(
    (companyId: string) => tasks.filter((t) => t.companyId === companyId),
    [tasks],
  );

  const addTask = useCallback(
    async (task: CompanyTask) => {
      await storage.saveTask(task);
      await refresh();
    },
    [refresh],
  );

  const updateTask = useCallback(
    async (id: string, updates: Partial<CompanyTask>) => {
      await storage.updateTask(id, updates);
      await refresh();
    },
    [refresh],
  );

  const deleteTask = useCallback(
    async (id: string) => {
      await storage.deleteTask(id);
      await refresh();
    },
    [refresh],
  );

  return {
    tasks,
    myTasks,
    loading,
    refresh,
    getByCompany,
    addTask,
    updateTask,
    deleteTask,
  };
}
