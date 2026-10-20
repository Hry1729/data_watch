import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { ChevronDown, KeyRound, Pencil, Plus, RotateCcw, Search, Trash2 } from "lucide-react";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Textarea } from "@/components/ui/textarea";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import type { MenuTreeItem, RoleGrant, RoleListItem, RolePayload, RoleQuery } from "@/types/system-management";

const emptyForm: RolePayload = { roleName: "", roleCode: "R_", description: "", enabled: true };

// 概念稿尺寸：控件 32px、表头 40px、行 44px、正文 13px
const fieldCls = "h-8 bg-background text-[13px] md:text-[13px]";
const selectCls = "!h-8 bg-background text-[13px]";
const btnCls = "h-8 gap-1.5 px-2.5 text-[13px] [&_svg:not([class*='size-'])]:size-3.5";
const priCls = "bg-brand text-brand-foreground hover:bg-brand/90";
const thCls = "h-10 px-2.5 text-[13px] font-medium text-muted-foreground";
const tdCls = "h-11 px-2.5 py-1";
const badgeCls = "h-5 rounded-md px-2 font-mono text-[11px]";
const switchCls = "data-[size=default]:h-5 data-[size=default]:w-[34px] data-checked:bg-brand [&_[data-slot=switch-thumb]]:!size-4";
const iconBtnCls = "size-7 text-muted-foreground hover:text-foreground [&_svg:not([class*='size-'])]:size-3.5";
const checkCls = "data-checked:border-brand data-checked:bg-brand dark:data-checked:bg-brand";

function countMenus(items: MenuTreeItem[] | undefined, menuIds: Set<number>): [number, number] {
  let done = 0; let total = 0;
  const visit = (list: MenuTreeItem[]) => list.forEach((menu) => { total += 1; if (menuIds.has(menu.id)) done += 1; if (menu.children) visit(menu.children); });
  visit(items ?? []);
  return [done, total];
}

function GrantNode({ menu, depth, menuIds, permissionIds, collapsed, toggleCollapsed, toggleMenu, togglePermission }: { menu: MenuTreeItem; depth: number; menuIds: Set<number>; permissionIds: Set<number>; collapsed: Set<number>; toggleCollapsed: (id: number) => void; toggleMenu: (id: number, checked: boolean) => void; togglePermission: (id: number, checked: boolean) => void }) {
  const permissions = (menu.meta.authList ?? []).filter((permission) => permission.permissionType === "action");
  const expandable = Boolean(menu.children?.length || permissions.length);
  const closed = collapsed.has(menu.id);
  const [done, total] = countMenus(menu.children, menuIds);
  return (
    <div>
      <div className={`flex min-h-[34px] items-center gap-2 rounded-md pr-2 text-[13px] hover:bg-card ${depth === 0 ? "font-medium" : ""}`} style={{ paddingLeft: `${depth * 20 + 8}px` }}>
        <button type="button" className={`grid size-4 shrink-0 place-items-center text-muted-foreground transition-transform ${expandable ? "" : "invisible"} ${closed ? "-rotate-90" : ""}`} title={closed ? "展开" : "收起"} onClick={() => toggleCollapsed(menu.id)}><ChevronDown className="size-3.5" /></button>
        <label className="flex min-w-0 flex-1 cursor-pointer items-center gap-2">
          <Checkbox className={checkCls} checked={menuIds.has(menu.id)} onCheckedChange={(checked) => toggleMenu(menu.id, checked === true)} />
          <span className="truncate">{menu.meta.title || menu.name}</span>
          {menu.enabled === false && <Badge variant="destructive" className="h-[18px] rounded-md px-1.5 text-[11px]">禁用</Badge>}
        </label>
        {total > 0 && <span className="ml-auto text-[11.5px] font-normal text-muted-foreground tabular-nums">{done}/{total}</span>}
      </div>
      {!closed && menu.children?.map((child) => <GrantNode key={child.id} menu={child} depth={depth + 1} menuIds={menuIds} permissionIds={permissionIds} collapsed={collapsed} toggleCollapsed={toggleCollapsed} toggleMenu={toggleMenu} togglePermission={togglePermission} />)}
      {!closed && permissions.map((permission) => (
        <label key={permission.id} className="flex min-h-[30px] cursor-pointer items-center gap-2 rounded-md pr-2 text-[12.5px] text-muted-foreground hover:bg-card" style={{ paddingLeft: `${(depth + 1) * 20 + 8 + 24}px` }}>
          <Checkbox className={checkCls} checked={permissionIds.has(permission.id)} onCheckedChange={(checked) => togglePermission(permission.id, checked === true)} />
          <KeyRound className="size-3.5" />
          <span>{permission.title}</span>
        </label>
      ))}
    </div>
  );
}

export function RoleManagementPage() {
  const { auth } = useAuth();
  const token = auth?.backendToken;
  const [draft, setDraft] = useState<RoleQuery>({});
  const [query, setQuery] = useState<RoleQuery>({ current: 1, size: 20 });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<RoleListItem | null>(null);
  const [form, setForm] = useState<RolePayload>(emptyForm);
  const [deleteRole, setDeleteRole] = useState<RoleListItem | null>(null);
  const [grantRole, setGrantRole] = useState<RoleListItem | null>(null);
  const [grant, setGrant] = useState<RoleGrant>({ menuIds: [], permissionIds: [], dataScopes: [] });
  const [grantLoading, setGrantLoading] = useState(false);
  // 授权数据所属角色 ID 与请求代号：旧角色的迟到响应不得写入当前角色的授权
  const [grantFor, setGrantFor] = useState<number | null>(null);
  const grantSeq = useRef(0);
  const [collapsed, setCollapsed] = useState<Set<number>>(new Set());

  const loadRoles = useCallback((activeToken: string, signal: AbortSignal) => backendClient.roleList(activeToken, query, signal), [query]);
  const roles = useBackendQuery(loadRoles);
  const loadMenus = useCallback((activeToken: string, signal: AbortSignal) => backendClient.adminMenus(activeToken, signal), []);
  const menus = useBackendQuery(loadMenus);
  const currentPage = query.current ?? 1;
  const pageSize = query.size ?? 20;
  const totalPages = Math.max(1, Math.ceil((roles.data?.total ?? 0) / pageSize));
  const menuIds = new Set(grant.menuIds);
  const permissionIds = new Set(grant.permissionIds);
  const [menuDone, menuTotal] = countMenus(menus.data ?? undefined, menuIds);
  const allPermissionIds: number[] = [];
  const collectPermissions = (items: MenuTreeItem[]) => items.forEach((menu) => { (menu.meta.authList ?? []).filter((permission) => permission.permissionType === "action").forEach((permission) => allPermissionIds.push(permission.id)); if (menu.children) collectPermissions(menu.children); });
  collectPermissions(menus.data ?? []);
  const permissionDone = allPermissionIds.filter((id) => permissionIds.has(id)).length;
  const firstRole = roles.data?.records?.[0];

  // 概念稿默认选中第一个角色，右侧常驻显示其菜单权限
  useEffect(() => {
    if (!grantRole && firstRole) void openGrant(firstRole);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firstRole, token]);

  function openCreate() { setEditing(null); setForm({ ...emptyForm }); setDialogOpen(true); }
  function openEdit(role: RoleListItem) { setEditing(role); setForm({ roleName: role.roleName, roleCode: role.roleCode, description: role.description, enabled: role.enabled }); setDialogOpen(true); }

  async function saveRole() {
    if (!token) return;
    if (form.roleName.trim().length < 2) return toast.error("角色名称至少需要 2 个字符");
    if (!/^R_[A-Z0-9_]+$/.test(form.roleCode)) return toast.error("角色编码需以 R_ 开头，仅使用大写字母、数字和下划线");
    try { if (editing) await backendClient.updateRole(token, editing.roleId, form); else await backendClient.createRole(token, form); toast.success(editing ? "角色修改成功" : "角色新增成功"); setDialogOpen(false); roles.refresh(); }
    catch { toast.error("角色保存失败"); }
  }

  async function toggleStatus(role: RoleListItem, enabled: boolean) {
    if (!token) return;
    if (role.roleCode === "R_SUPER" && !enabled) return toast.warning("超级管理员角色不能禁用");
    try { await backendClient.updateRoleStatus(token, role.roleId, enabled); toast.success(enabled ? "启用角色成功" : "禁用角色成功"); roles.refresh(); }
    catch { toast.error("角色状态更新失败"); }
  }

  async function confirmDelete() {
    if (!token || !deleteRole) return;
    try { await backendClient.deleteRole(token, deleteRole.roleId); toast.success("角色已删除"); setDeleteRole(null); roles.refresh(); }
    catch { toast.error("角色删除失败"); }
  }

  async function openGrant(role: RoleListItem) {
    if (!token) return;
    const seq = ++grantSeq.current;
    setGrantRole(role); setGrantFor(null); setGrantLoading(true);
    try { const current = await backendClient.roleGrant(token, role.roleId); if (seq !== grantSeq.current) return; setGrant(current); setGrantFor(role.roleId); }
    catch { if (seq !== grantSeq.current) return; toast.error("菜单权限加载失败"); setGrantRole(null); }
    finally { if (seq === grantSeq.current) setGrantLoading(false); }
  }

  async function saveGrant() {
    if (!token || !grantRole || grantLoading || grantFor !== grantRole.roleId) return;
    try { await backendClient.updateRoleGrant(token, grantFor, grant); toast.success("菜单权限已保存"); }
    catch { toast.error("权限保存失败"); }
  }

  function toggleCollapsed(id: number) { setCollapsed((current) => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }
  function toggleMenu(id: number, checked: boolean) { setGrant((value) => ({ ...value, menuIds: checked ? [...new Set([...value.menuIds, id])] : value.menuIds.filter((item) => item !== id) })); }
  function togglePermission(id: number, checked: boolean) { setGrant((value) => ({ ...value, permissionIds: checked ? [...new Set([...value.permissionIds, id])] : value.permissionIds.filter((item) => item !== id) })); }
  function selectAll(checked: boolean) {
    const allMenus: number[] = []; const allPermissions: number[] = [];
    const visit = (items: MenuTreeItem[]) => items.forEach((menu) => { allMenus.push(menu.id); (menu.meta.authList ?? []).forEach((permission) => allPermissions.push(permission.id)); if (menu.children) visit(menu.children); });
    visit(menus.data ?? []);
    setGrant((value) => ({ ...value, menuIds: checked ? allMenus : [], permissionIds: checked ? allPermissions : [] }));
  }

  return (
    <div className="grid h-full min-h-[560px] w-full grid-cols-[minmax(0,1fr)_352px] grid-rows-[minmax(0,1fr)] items-stretch gap-4">
      <Card className="flex min-h-0 flex-col gap-0 py-0">
        <div className="flex items-center gap-2 px-5 pt-5">
          <div className="relative">
            <Search className="pointer-events-none absolute top-1/2 left-2.5 size-3.5 -translate-y-1/2 text-muted-foreground" />
            <Input className={`${fieldCls} w-[116px] pl-8`} value={draft.roleName ?? ""} onChange={(event) => setDraft((value) => ({ ...value, roleName: event.target.value }))} placeholder="角色名称" />
          </div>
          <Input className={`${fieldCls} w-[100px]`} value={draft.roleCode ?? ""} onChange={(event) => setDraft((value) => ({ ...value, roleCode: event.target.value }))} placeholder="角色编码" />
          <Input className={`${fieldCls} w-[108px]`} value={draft.description ?? ""} onChange={(event) => setDraft((value) => ({ ...value, description: event.target.value }))} placeholder="角色描述" />
          <Select value={draft.enabled === undefined || draft.enabled === "" ? "all" : String(draft.enabled)} onValueChange={(value) => setDraft((current) => ({ ...current, enabled: value === "all" ? "" : value === "true" }))}>
            <SelectTrigger className={`${selectCls} w-[120px]`}><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">状态（全部）</SelectItem>
              <SelectItem value="true">启用</SelectItem>
              <SelectItem value="false">禁用</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" className={btnCls} onClick={() => { setDraft({}); setQuery({ current: 1, size: pageSize }); }}><RotateCcw data-icon="inline-start" />重置</Button>
          <Button variant="outline" className={btnCls} onClick={() => setQuery({ ...draft, current: 1, size: pageSize })}><Search data-icon="inline-start" />查询</Button>
          <Button className={`${btnCls} ${priCls} ml-auto`} onClick={openCreate}><Plus data-icon="inline-start" />新增角色</Button>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col px-5 pt-3 pb-5">
          <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-[10px] border">
            <div className="min-h-0 flex-1 overflow-auto [&_[data-slot=table-container]]:overflow-visible">
              <Table className="text-[13px]">
                <TableHeader className="sticky top-0 z-10 bg-[color-mix(in_oklab,var(--muted)_55%,var(--card))]">
                  <TableRow className="hover:bg-transparent">
                    <TableHead className={`${thCls} w-16`}>角色ID</TableHead>
                    <TableHead className={thCls}>角色名称</TableHead>
                    <TableHead className={thCls}>角色编码</TableHead>
                    <TableHead className={thCls}>角色描述</TableHead>
                    <TableHead className={thCls}>状态</TableHead>
                    <TableHead className={thCls}>创建日期</TableHead>
                    <TableHead className={thCls}>操作</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {roles.loading && Array.from({ length: 5 }, (_, index) => <TableRow key={index}><TableCell colSpan={7} className="h-11 px-2.5"><Skeleton className="h-7 w-full" /></TableCell></TableRow>)}
                  {!roles.loading && (roles.data?.records ?? []).map((role) => {
                    const selected = grantRole?.roleId === role.roleId;
                    const [date, time] = (role.createTime?.slice(0, 19) ?? "").replace("T", " ").split(" ");
                    return (
                      <TableRow key={role.roleId} className={`cursor-pointer ${selected ? "bg-brand/[0.07] hover:bg-brand/[0.07]" : ""}`} onClick={() => { if (!selected) void openGrant(role); }}>
                        <TableCell className={`${tdCls} text-muted-foreground ${selected ? "shadow-[inset_3px_0_0_var(--brand)]" : ""}`}>{role.roleId}</TableCell>
                        <TableCell className={`${tdCls} font-medium`}>{role.roleName}</TableCell>
                        <TableCell className={tdCls}><Badge variant="outline" className={role.roleCode === "R_SUPER" ? `${badgeCls} border-transparent bg-brand-soft text-brand` : `${badgeCls} bg-transparent text-foreground`}>{role.roleCode}</Badge></TableCell>
                        <TableCell className={`${tdCls} min-w-[120px] leading-snug whitespace-normal text-muted-foreground`}>{role.description || "-"}</TableCell>
                        <TableCell className={tdCls} onClick={(event) => event.stopPropagation()}>
                          <div className="flex items-center gap-2">
                            <Switch className={switchCls} checked={role.enabled} onCheckedChange={(enabled) => void toggleStatus(role, enabled)} />
                            <span className="text-xs text-muted-foreground">{role.enabled ? "启用" : "禁用"}</span>
                          </div>
                        </TableCell>
                        <TableCell className={`${tdCls} font-mono text-xs leading-tight`}><div>{date}</div><div className="text-muted-foreground">{time}</div></TableCell>
                        <TableCell className={tdCls} onClick={(event) => event.stopPropagation()}>
                          <div className="flex gap-0.5">
                            <Button size="icon" variant="ghost" className={`${iconBtnCls} ${selected ? "bg-brand-soft text-brand hover:bg-brand-soft hover:text-brand" : ""}`} title="菜单权限" onClick={() => void openGrant(role)}><KeyRound /></Button>
                            <Button size="icon" variant="ghost" className={iconBtnCls} title="编辑角色" onClick={() => openEdit(role)}><Pencil /></Button>
                            <Button size="icon" variant="ghost" className={`${iconBtnCls} hover:bg-destructive-soft hover:text-destructive disabled:opacity-35`} title="删除角色" disabled={role.roleCode === "R_SUPER"} onClick={() => setDeleteRole(role)}><Trash2 /></Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                  {!roles.loading && !roles.data?.records?.length && <TableRow><TableCell colSpan={7} className="h-32 text-center text-muted-foreground">{roles.error ?? "暂无角色"}</TableCell></TableRow>}
                </TableBody>
              </Table>
            </div>
            <div className="flex shrink-0 items-center justify-between gap-2 border-t px-3 py-2.5 text-xs text-muted-foreground">
              <span>共 {roles.data?.total ?? 0} 条</span>
              <div className="flex items-center gap-2">
                <Button size="sm" variant="outline" className="h-7 px-2.5 text-xs" disabled={currentPage <= 1} onClick={() => setQuery((value) => ({ ...value, current: currentPage - 1 }))}>上一页</Button>
                <span>第 {currentPage} / {totalPages} 页</span>
                <Button size="sm" variant="outline" className="h-7 px-2.5 text-xs" disabled={currentPage >= totalPages} onClick={() => setQuery((value) => ({ ...value, current: currentPage + 1 }))}>下一页</Button>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="flex min-h-0 flex-col gap-0 py-0">
        <div className="px-4 pt-4">
          <p className="truncate text-sm leading-none font-semibold">菜单权限{grantRole ? ` · ${grantRole.roleName}` : ""}</p>
          <p className="mt-1.5 text-[13px] text-muted-foreground">菜单与按钮权限独立勾选</p>
        </div>
        <div className="flex items-center justify-between gap-2 px-4 pt-3.5 pb-2 text-xs whitespace-nowrap text-muted-foreground">
          <span>菜单 <b className="font-semibold text-foreground tabular-nums">{grantRole ? menuDone : 0} / {menuTotal}</b> · 按钮权限 <b className="font-semibold text-foreground tabular-nums">{grantRole ? permissionDone : 0} / {allPermissionIds.length}</b></span>
          <div className="flex gap-1.5">
            <Button size="sm" variant="outline" className="h-7 px-2 text-xs" disabled={!grantRole || grantLoading} onClick={() => selectAll(true)}>全部选择</Button>
            <Button size="sm" variant="outline" className="h-7 px-2 text-xs" disabled={!grantRole || grantLoading} onClick={() => selectAll(false)}>取消全选</Button>
          </div>
        </div>
        <div className="mx-4 min-h-0 flex-1 overflow-y-auto rounded-[10px] bg-[color-mix(in_oklab,var(--muted)_55%,transparent)] p-1">
          {!grantRole ? (
            <div className="flex h-full min-h-40 items-center justify-center text-[13px] text-muted-foreground">选择左侧角色的菜单权限</div>
          ) : grantLoading ? (
            <div className="space-y-2 p-3"><Skeleton className="h-8 w-full" /><Skeleton className="h-8 w-5/6" /></div>
          ) : (
            (menus.data ?? []).map((menu) => <GrantNode key={menu.id} menu={menu} depth={0} menuIds={menuIds} permissionIds={permissionIds} collapsed={collapsed} toggleCollapsed={toggleCollapsed} toggleMenu={toggleMenu} togglePermission={togglePermission} />)
          )}
        </div>
        <div className="flex shrink-0 justify-end gap-2 px-4 pt-3 pb-4">
          <Button variant="outline" className={btnCls} disabled={!grantRole || grantLoading} onClick={() => { if (grantRole) void openGrant(grantRole); }}>取消</Button>
          <Button className={`${btnCls} ${priCls}`} onClick={() => void saveGrant()} disabled={!grantRole || grantLoading || grantFor !== grantRole.roleId}>保存</Button>
        </div>
      </Card>

    <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑角色" : "新增角色"}</DialogTitle></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label>角色名称</Label><Input value={form.roleName} onChange={(event) => setForm((value) => ({ ...value, roleName: event.target.value }))} /></div><div className="space-y-2"><Label>角色编码</Label><Input disabled={Boolean(editing)} value={form.roleCode} onChange={(event) => setForm((value) => ({ ...value, roleCode: event.target.value.toUpperCase() }))} placeholder="如 R_TEST_001" /></div><div className="space-y-2"><Label>描述</Label><Textarea rows={3} value={form.description ?? ""} onChange={(event) => setForm((value) => ({ ...value, description: event.target.value }))} /></div><div className="flex items-center justify-between rounded-md border p-3"><div><p className="text-sm font-medium">角色状态</p><p className="text-xs text-muted-foreground">启用后可分配给用户</p></div><Switch checked={form.enabled} onCheckedChange={(enabled) => setForm((value) => ({ ...value, enabled }))} /></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={() => void saveRole()}>提交</Button></DialogFooter></DialogContent></Dialog>
    <AlertDialog open={Boolean(deleteRole)} onOpenChange={(open) => { if (!open) setDeleteRole(null); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>删除角色</AlertDialogTitle><AlertDialogDescription>确定删除角色“{deleteRole?.roleName}”吗？已关联用户可能受到影响。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={() => void confirmDelete()}>确认删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}

export default RoleManagementPage;
