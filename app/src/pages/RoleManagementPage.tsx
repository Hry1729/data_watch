import { useCallback, useState } from "react";
import { toast } from "sonner";
import { KeyRound, Pencil, Plus, Search, ShieldCheck, Trash2 } from "lucide-react";
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

function GrantNode({ menu, depth, menuIds, permissionIds, toggleMenu, togglePermission }: { menu: MenuTreeItem; depth: number; menuIds: Set<number>; permissionIds: Set<number>; toggleMenu: (id: number, checked: boolean) => void; togglePermission: (id: number, checked: boolean) => void }) {
  return <div><label className="flex min-h-9 items-center gap-2 rounded-sm px-2 hover:bg-muted/60" style={{ paddingLeft: `${depth * 20 + 8}px` }}><Checkbox checked={menuIds.has(menu.id)} onCheckedChange={(checked) => toggleMenu(menu.id, checked === true)} /><ShieldCheck className="size-4 text-muted-foreground" /><span className="text-sm font-medium">{menu.meta.title || menu.name}</span>{menu.enabled === false && <Badge variant="destructive">禁用</Badge>}</label>{menu.children?.map((child) => <GrantNode key={child.id} menu={child} depth={depth + 1} menuIds={menuIds} permissionIds={permissionIds} toggleMenu={toggleMenu} togglePermission={togglePermission} />)}{(menu.meta.authList ?? []).filter((permission) => permission.permissionType === "action").map((permission) => <label key={permission.id} className="flex min-h-8 items-center gap-2 rounded-sm px-2 text-sm text-muted-foreground hover:bg-muted/60" style={{ paddingLeft: `${(depth + 1) * 20 + 8}px` }}><Checkbox checked={permissionIds.has(permission.id)} onCheckedChange={(checked) => togglePermission(permission.id, checked === true)} /><KeyRound className="size-3.5" /><span>{permission.title}</span><Badge variant="outline">权限</Badge></label>)}</div>;
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

  const loadRoles = useCallback((activeToken: string, signal: AbortSignal) => backendClient.roleList(activeToken, query, signal), [query]);
  const roles = useBackendQuery(loadRoles);
  const loadMenus = useCallback((activeToken: string, signal: AbortSignal) => backendClient.adminMenus(activeToken, signal), []);
  const menus = useBackendQuery(loadMenus);
  const currentPage = query.current ?? 1;
  const pageSize = query.size ?? 20;
  const totalPages = Math.max(1, Math.ceil((roles.data?.total ?? 0) / pageSize));
  const menuIds = new Set(grant.menuIds);
  const permissionIds = new Set(grant.permissionIds);

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
    setGrantRole(role); setGrantLoading(true);
    try { const current = await backendClient.roleGrant(token, role.roleId); setGrant(current); }
    catch { toast.error("菜单权限加载失败"); setGrantRole(null); }
    finally { setGrantLoading(false); }
  }

  async function saveGrant() {
    if (!token || !grantRole) return;
    try { await backendClient.updateRoleGrant(token, grantRole.roleId, grant); toast.success("菜单权限已保存"); }
    catch { toast.error("权限保存失败"); }
  }

  function toggleMenu(id: number, checked: boolean) { setGrant((value) => ({ ...value, menuIds: checked ? [...new Set([...value.menuIds, id])] : value.menuIds.filter((item) => item !== id) })); }
  function togglePermission(id: number, checked: boolean) { setGrant((value) => ({ ...value, permissionIds: checked ? [...new Set([...value.permissionIds, id])] : value.permissionIds.filter((item) => item !== id) })); }
  function selectAll(checked: boolean) {
    const allMenus: number[] = []; const allPermissions: number[] = [];
    const visit = (items: MenuTreeItem[]) => items.forEach((menu) => { allMenus.push(menu.id); (menu.meta.authList ?? []).forEach((permission) => allPermissions.push(permission.id)); if (menu.children) visit(menu.children); });
    visit(menus.data ?? []);
    setGrant((value) => ({ ...value, menuIds: checked ? allMenus : [], permissionIds: checked ? allPermissions : [] }));
  }

  return (
    <div className="grid h-full min-h-0 w-full gap-4 lg:grid-cols-[minmax(0,1.25fr)_minmax(380px,1fr)]">
      <Card className="flex min-h-0 flex-col gap-0 py-0">
        <div className="flex flex-wrap items-center gap-2 px-4 pt-4">
          <Input className="h-7 w-40" value={draft.roleName ?? ""} onChange={(event) => setDraft((value) => ({ ...value, roleName: event.target.value }))} placeholder="角色名称" />
          <Input className="h-7 w-32" value={draft.roleCode ?? ""} onChange={(event) => setDraft((value) => ({ ...value, roleCode: event.target.value }))} placeholder="角色编码" />
          <Input className="h-7 w-36" value={draft.description ?? ""} onChange={(event) => setDraft((value) => ({ ...value, description: event.target.value }))} placeholder="角色描述" />
          <Select value={draft.enabled === undefined || draft.enabled === "" ? "all" : String(draft.enabled)} onValueChange={(value) => setDraft((current) => ({ ...current, enabled: value === "all" ? "" : value === "true" }))}>
            <SelectTrigger className="!h-7 w-32"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">状态（全部）</SelectItem>
              <SelectItem value="true">启用</SelectItem>
              <SelectItem value="false">禁用</SelectItem>
            </SelectContent>
          </Select>
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            <Button size="sm" variant="outline" onClick={() => { setDraft({}); setQuery({ current: 1, size: pageSize }); }}>重置</Button>
            <Button size="sm" variant="outline" onClick={() => setQuery({ ...draft, current: 1, size: pageSize })}><Search data-icon="inline-start" />查询</Button>
            <Button size="sm" onClick={openCreate}><Plus data-icon="inline-start" />新增角色</Button>
          </div>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3 pt-3">
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <Table>
              <TableHeader className="bg-muted/50">
                <TableRow>
                  <TableHead className="text-center">角色ID</TableHead>
                  <TableHead className="text-center">角色名称</TableHead>
                  <TableHead className="text-center">角色编码</TableHead>
                  <TableHead className="text-center">角色描述</TableHead>
                  <TableHead className="text-center">状态</TableHead>
                  <TableHead className="text-center">创建日期</TableHead>
                  <TableHead className="text-center">操作</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {roles.loading && Array.from({ length: 5 }, (_, index) => <TableRow key={index}><TableCell colSpan={7}><Skeleton className="h-8 w-full" /></TableCell></TableRow>)}
                {!roles.loading && (roles.data?.records ?? []).map((role) => (
                  <TableRow key={role.roleId}>
                    <TableCell>{role.roleId}</TableCell>
                    <TableCell className="font-medium">{role.roleName}</TableCell>
                    <TableCell><Badge variant="outline">{role.roleCode}</Badge></TableCell>
                    <TableCell>{role.description || "-"}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Switch checked={role.enabled} onCheckedChange={(enabled) => void toggleStatus(role, enabled)} />
                        <span className="text-xs text-muted-foreground">{role.enabled ? "启用" : "禁用"}</span>
                      </div>
                    </TableCell>
                    <TableCell className="whitespace-nowrap">{role.createTime?.slice(0, 19)}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <Button size="icon" variant="ghost" title="菜单权限" onClick={() => void openGrant(role)}><KeyRound className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="编辑角色" onClick={() => openEdit(role)}><Pencil className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="删除角色" disabled={role.roleCode === "R_SUPER"} onClick={() => setDeleteRole(role)}><Trash2 className="size-4" /></Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!roles.loading && !roles.data?.records?.length && <TableRow><TableCell colSpan={7} className="h-32 text-center text-muted-foreground">{roles.error ?? "暂无角色"}</TableCell></TableRow>}
              </TableBody>
            </Table>
          </div>
          <div className="flex flex-col gap-3 pt-1 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
            <span>共 {roles.data?.total ?? 0} 条</span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" disabled={currentPage <= 1} onClick={() => setQuery((value) => ({ ...value, current: currentPage - 1 }))}>上一页</Button>
              <span>第 {currentPage} / {totalPages} 页</span>
              <Button size="sm" variant="outline" disabled={currentPage >= totalPages} onClick={() => setQuery((value) => ({ ...value, current: currentPage + 1 }))}>下一页</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Card className="flex min-h-0 flex-col gap-0 py-0">
        <div className="flex flex-wrap items-center gap-2 px-4 pt-4">
          <span className="text-sm font-medium">菜单权限</span>
          {grantRole && <span className="truncate text-xs text-muted-foreground">· {grantRole.roleName}</span>}
          <div className="ml-auto flex items-center gap-2">
            <Button size="sm" variant="outline" disabled={!grantRole || grantLoading} onClick={() => selectAll(true)}>全部选择</Button>
            <Button size="sm" variant="outline" disabled={!grantRole || grantLoading} onClick={() => selectAll(false)}>取消全选</Button>
          </div>
        </div>
        <p className="mt-2 px-4 text-xs text-muted-foreground">菜单与按钮权限独立勾选</p>
        <div className="mx-4 mt-3 min-h-0 flex-1 overflow-y-auto rounded-lg bg-muted/40 p-2">
          {!grantRole ? (
            <div className="flex h-full min-h-40 items-center justify-center text-sm text-muted-foreground">选择左侧角色的菜单权限</div>
          ) : grantLoading ? (
            <div className="space-y-2 p-3"><Skeleton className="h-8 w-full" /><Skeleton className="h-8 w-5/6" /></div>
          ) : (
            (menus.data ?? []).map((menu) => <GrantNode key={menu.id} menu={menu} depth={0} menuIds={menuIds} permissionIds={permissionIds} toggleMenu={toggleMenu} togglePermission={togglePermission} />)
          )}
        </div>
        <div className="mt-3 flex justify-end border-t px-4 py-3">
          <Button size="sm" onClick={() => void saveGrant()} disabled={!grantRole || grantLoading}>保存</Button>
        </div>
      </Card>

    <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑角色" : "新增角色"}</DialogTitle></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label>角色名称</Label><Input value={form.roleName} onChange={(event) => setForm((value) => ({ ...value, roleName: event.target.value }))} /></div><div className="space-y-2"><Label>角色编码</Label><Input disabled={Boolean(editing)} value={form.roleCode} onChange={(event) => setForm((value) => ({ ...value, roleCode: event.target.value.toUpperCase() }))} placeholder="如 R_TEST_001" /></div><div className="space-y-2"><Label>描述</Label><Textarea rows={3} value={form.description ?? ""} onChange={(event) => setForm((value) => ({ ...value, description: event.target.value }))} /></div><div className="flex items-center justify-between rounded-md border p-3"><div><p className="text-sm font-medium">角色状态</p><p className="text-xs text-muted-foreground">启用后可分配给用户</p></div><Switch checked={form.enabled} onCheckedChange={(enabled) => setForm((value) => ({ ...value, enabled }))} /></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={() => void saveRole()}>提交</Button></DialogFooter></DialogContent></Dialog>
    <AlertDialog open={Boolean(deleteRole)} onOpenChange={(open) => { if (!open) setDeleteRole(null); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>删除角色</AlertDialogTitle><AlertDialogDescription>确定删除角色“{deleteRole?.roleName}”吗？已关联用户可能受到影响。</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={() => void confirmDelete()}>确认删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}

export default RoleManagementPage;
