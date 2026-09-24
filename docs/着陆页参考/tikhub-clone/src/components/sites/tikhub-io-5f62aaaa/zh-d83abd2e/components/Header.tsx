"use client";

import { useState } from "react";
import Image from "next/image";
import { ChevronDownIcon, SearchIcon } from "../../shared/icons";
import { ThemeToggle } from "./ThemeToggle";

const NAV_ITEMS: {
  label: string;
  hasDropdown: boolean;
  children: { label: string; href: string }[];
}[] = [
  {
    label: "产品",
    hasDropdown: true,
    children: [
      { label: "数据 API", href: "#" },
      { label: "数据集", href: "#" },
      { label: "MCP 服务", href: "#" },
      { label: "合作伙伴", href: "#" },
      { label: "市场", href: "#" },
    ],
  },
  {
    label: "解决方案",
    hasDropdown: true,
    children: [
      { label: "AI 训练数据", href: "#" },
      { label: "市场情报", href: "#" },
      { label: "品牌监控", href: "#" },
      { label: "内容运营", href: "#" },
    ],
  },
  {
    label: "开发者",
    hasDropdown: true,
    children: [
      { label: "API 文档", href: "#" },
      { label: "SDK 下载", href: "#" },
      { label: "状态页", href: "#" },
      { label: "变更日志", href: "#" },
    ],
  },
  { label: "定价", hasDropdown: false, children: [] },
  { label: "企业版", hasDropdown: false, children: [] },
];

function NavDropdown({
  label,
  children,
}: {
  label: string;
  children: { label: string; href: string }[];
}) {
  const [open, setOpen] = useState(false);
  return (
    <div
      className="relative"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button
        type="button"
        className="inline-flex items-center gap-1 rounded-md px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted"
      >
        {label}
        <ChevronDownIcon className="h-3.5 w-3.5 opacity-60" />
      </button>
      {open && (
        <div className="absolute left-0 top-full z-20 min-w-[180px] rounded-lg border border-border bg-surface p-1 shadow-lg">
          {children.map((item) => (
            <a
              key={item.label}
              href={item.href}
              className="block rounded-md px-3 py-2 text-sm text-foreground transition-colors hover:bg-muted"
            >
              {item.label}
            </a>
          ))}
        </div>
      )}
    </div>
  );
}

export function Header() {
  const [mobileOpen, setMobileOpen] = useState(false);

  return (
    <header className="relative z-10">
      <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between gap-4">
        {/* Logo */}
        <a href="/" className="flex items-center gap-2 shrink-0">
          <Image
            src="/sites/tikhub-io-5f62aaaa/shared/logo.jpeg"
            alt="TikHub"
            width={32}
            height={32}
            className="rounded-md"
          />
          <span className="text-base font-semibold tracking-tight text-foreground">
            TikHub
          </span>
        </a>

        {/* Desktop nav */}
        <nav className="hidden md:flex items-center gap-1">
          {NAV_ITEMS.map((item) =>
            item.hasDropdown ? (
              <NavDropdown key={item.label} label={item.label}>
                {item.children}
              </NavDropdown>
            ) : (
              <a
                key={item.label}
                href="#"
                className="rounded-md px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted"
              >
                {item.label}
              </a>
            ),
          )}
        </nav>

        {/* Right tools — desktop */}
        <div className="hidden md:flex items-center gap-2">
          <button
            type="button"
            className="inline-flex items-center gap-2 rounded-md border border-border bg-surface px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted"
            aria-label="搜索"
          >
            <SearchIcon className="h-4 w-4" />
            <span>搜索</span>
            <kbd className="hidden lg:inline-flex items-center rounded border border-border bg-muted px-1.5 py-0.5 text-[10px] font-mono text-muted-foreground">
              ⌘K
            </kbd>
          </button>
          <ThemeToggle />
          <button
            type="button"
            className="inline-flex items-center gap-1 rounded-md border border-border bg-surface px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted"
            aria-label="选择语言"
          >
            CN 中文
            <ChevronDownIcon className="h-3.5 w-3.5 opacity-60" />
          </button>
          <a
            href="https://user.tikhub.io/"
            target="_blank"
            rel="noreferrer"
            className="rounded-md px-3 py-2 text-sm font-medium text-foreground transition-colors hover:bg-muted"
          >
            登录
          </a>
          <a
            href="https://calendar.app.google/AQLMWVdfAksrWeHh9"
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center rounded-md bg-inverse px-3 py-2 text-sm font-medium transition-transform hover:scale-105"
          >
            预约通话
          </a>
        </div>

        {/* Mobile right tools */}
        <div className="flex md:hidden items-center gap-2">
          <button
            type="button"
            className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-surface transition-colors hover:bg-muted"
            aria-label="搜索"
          >
            <SearchIcon className="h-4 w-4" />
          </button>
          <ThemeToggle />
          <button
            type="button"
            onClick={() => setMobileOpen((v) => !v)}
            className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-surface transition-colors hover:bg-muted"
            aria-label="切换菜单"
            aria-expanded={mobileOpen}
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-4 w-4"
            >
              {mobileOpen ? (
                <path d="M18 6 6 18M6 6l12 12" />
              ) : (
                <>
                  <path d="M3 6h18" />
                  <path d="M3 12h18" />
                  <path d="M3 18h18" />
                </>
              )}
            </svg>
          </button>
        </div>
      </div>

      {/* Mobile nav panel */}
      {mobileOpen && (
        <div className="md:hidden border-t border-border bg-surface">
          <nav className="max-w-7xl mx-auto px-6 py-4 flex flex-col gap-1">
            {NAV_ITEMS.map((item) => (
              <details key={item.label} className="group">
                <summary
                  className={`flex items-center justify-between rounded-md px-3 py-2 text-sm font-medium text-foreground cursor-pointer hover:bg-muted ${item.hasDropdown ? "" : "list-none"}`}
                >
                  <span>{item.label}</span>
                  {item.hasDropdown && (
                    <ChevronDownIcon className="h-3.5 w-3.5 opacity-60 transition-transform group-open:rotate-180" />
                  )}
                </summary>
                {item.hasDropdown && (
                  <div className="mt-1 ml-3 flex flex-col gap-1 border-l border-border pl-3">
                    {item.children.map((child) => (
                      <a
                        key={child.label}
                        href={child.href}
                        className="rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted"
                      >
                        {child.label}
                      </a>
                    ))}
                  </div>
                )}
              </details>
            ))}
            <div className="mt-3 flex flex-col gap-2 border-t border-border pt-3">
              <button
                type="button"
                className="inline-flex items-center justify-center gap-1 rounded-md border border-border bg-surface px-3 py-2 text-sm font-medium text-foreground"
              >
                CN 中文
                <ChevronDownIcon className="h-3.5 w-3.5 opacity-60" />
              </button>
              <a
                href="https://user.tikhub.io/"
                target="_blank"
                rel="noreferrer"
                className="rounded-md border border-border bg-surface px-3 py-2 text-center text-sm font-medium text-foreground"
              >
                登录
              </a>
              <a
                href="https://calendar.app.google/AQLMWVdfAksrWeHh9"
                target="_blank"
                rel="noreferrer"
                className="rounded-md bg-inverse px-3 py-2 text-center text-sm font-medium"
              >
                预约通话
              </a>
            </div>
          </nav>
        </div>
      )}
    </header>
  );
}
