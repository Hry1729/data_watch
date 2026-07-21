import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";

interface PageRefreshValue {
  refreshVersion: number;
  refreshCurrentPage: () => void;
}

const PageRefreshContext = createContext<PageRefreshValue | null>(null);

export function PageRefreshProvider({ children }: PropsWithChildren) {
  const [refreshVersion, setRefreshVersion] = useState(0);
  const refreshCurrentPage = useCallback(() => {
    setRefreshVersion((version) => version + 1);
  }, []);
  const value = useMemo(
    () => ({ refreshVersion, refreshCurrentPage }),
    [refreshCurrentPage, refreshVersion],
  );

  return (
    <PageRefreshContext.Provider value={value}>
      {children}
    </PageRefreshContext.Provider>
  );
}

export function usePageRefresh() {
  const context = useContext(PageRefreshContext);
  if (!context) {
    throw new Error("usePageRefresh must be used within PageRefreshProvider");
  }
  return context;
}
