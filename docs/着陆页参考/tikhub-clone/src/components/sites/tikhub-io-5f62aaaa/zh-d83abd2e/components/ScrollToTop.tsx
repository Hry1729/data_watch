"use client";

import { useEffect, useState } from "react";
import { ArrowUpIcon } from "../../shared/icons";

export function ScrollToTop() {
  const [show, setShow] = useState(false);
  useEffect(() => {
    const onScroll = () => setShow(window.scrollY > 600);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);
  if (!show) return null;
  return (
    <button
      type="button"
      onClick={() => window.scrollTo({ top: 0, behavior: "smooth" })}
      className="fixed bottom-6 right-6 z-50 flex h-11 w-11 items-center justify-center rounded-full bg-inverse text-inverse-foreground shadow-lg transition-transform hover:scale-110"
      aria-label="回到顶部"
    >
      <ArrowUpIcon className="h-5 w-5" />
    </button>
  );
}
