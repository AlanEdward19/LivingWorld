import { useCallback, useRef, useState, type SetStateAction } from "react";

export function useEditorHistory<T>(initialValue: T) {
  const [value, setValue] = useState(initialValue);
  const past = useRef<T[]>([]);
  const future = useRef<T[]>([]);

  const commit = useCallback((action: SetStateAction<T>) => {
    setValue((current) => {
      const next = typeof action === "function" ? (action as (value: T) => T)(current) : action;
      if (Object.is(current, next)) return current;
      past.current.push(current);
      future.current = [];
      return next;
    });
  }, []);

  const undo = useCallback(() => {
    setValue((current) => {
      const previous = past.current.pop();
      if (previous === undefined) return current;
      future.current.push(current);
      return previous;
    });
  }, []);

  const redo = useCallback(() => {
    setValue((current) => {
      const next = future.current.pop();
      if (next === undefined) return current;
      past.current.push(current);
      return next;
    });
  }, []);

  return { value, commit, undo, redo, canUndo: past.current.length > 0, canRedo: future.current.length > 0 };
}
