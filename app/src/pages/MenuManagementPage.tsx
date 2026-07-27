import { useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { ChevronDown, ChevronRight, KeyRound, Pencil, Plus, Trash2 } from "lucide-react";
import { AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle } from "@/components/ui/alert-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import type { MenuPayload, MenuTreeItem, PermissionItem, PermissionPayload } from "@/types/system-management";

const emptyMenu: MenuPayload = { name: "", path: "", component: "", titleKey: "", icon: "", menuType: "menu", sort: 1, enabled: true, keepAlive: false, isHide: false, isHideTab: false };
const emptyPermission: PermissionPayload = { permissionCode: "", permissionName: "", permissionType: "action", authMark: "", sort: 1, enabled: true };

interface FlatMenu { menu: MenuTreeItem; depth: number; }

function flattenMenus(items: MenuTreeItem[], expanded: Set<number>, depth = 0): FlatMenu[] {
  const rows: FlatMenu[] = [];
  for (const menu of items) {
    rows.push({ menu, depth });
    if (menu.children?.length && expanded.has(menu.id)) rows.push(...flattenMenus(menu.children, expanded, depth + 1));
  }
  return rows;
}

export function MenuManagementPage() {
  const { auth } = useAuth();
  const token = auth?.backendToken;
  const loadMenus = useCallback((activeToken: string) => backendClient.adminMenus(activeToken), []);
  const menus = useBackendQuery(loadMenus);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [menuOpen, setMenuOpen] = useState(false);
  const [editingMenu, setEditingMenu] = useState<MenuTreeItem | null>(null);
  const [menuForm, setMenuForm] = useState<MenuPayload>(emptyMenu);
  const [permissionOpen, setPermissionOpen] = useState(false);
  const [permissionMenu, setPermissionMenu] = useState<MenuTreeItem | null>(null);
  const [editingPermission, setEditingPermission] = useState<PermissionItem | null>(null);
  const [permissionForm, setPermissionForm] = useState<PermissionPayload>(emptyPermission);
  const [confirm, setConfirm] = useState<{ type: "menu"; menu: MenuTreeItem } | { type: "permission"; menu: MenuTreeItem; permission: PermissionItem } | null>(null);

  const rows = useMemo(() => flattenMenus(menus.data ?? [], expanded), [expanded, menus.data]);

  useEffect(() => {
    if (menus.data && expanded.size === 0) setExpanded(new Set(menus.data.map((menu) => menu.id)));
  }, [expanded.size, menus.data]);

  function openAdd(parent?: MenuTreeItem) {
    setEditingMenu(null);
    setMenuForm({ ...emptyMenu, parentId: parent?.id });
    setMenuOpen(true);
  }

  function openEdit(menu: MenuTreeItem) {
    setEditingMenu(menu);
    setMenuForm({ name: menu.name, path: menu.path, component: menu.component, titleKey: menu.meta.title, icon: menu.meta.icon ?? "", parentId: menu.parentId, menuType: menu.menuType ?? "menu", sort: menu.sort ?? 1, enabled: menu.enabled ?? true, keepAlive: menu.meta.keepAlive ?? false, isHide: menu.meta.isHide ?? false, isHideTab: menu.meta.isHideTab ?? false });
    setMenuOpen(true);
  }

  async function saveMenu() {
    if (!token) return;
    if (!menuForm.name.trim() || !menuForm.titleKey.trim() || !menuForm.path.trim()) return toast.error("请填写菜单名称、标题和路由地址");
    try { if (editingMenu) await backendClient.updateMenu(token, editingMenu.id, menuForm); else await backendClient.createMenu(token, menuForm); toast.success(editingMenu ? "菜单修改成功" : "菜单新增成功"); setMenuOpen(false); menus.refresh(); }
    catch { toast.error("菜单保存失败"); }
  }

  async function openPermission(menu: MenuTreeItem, permission?: PermissionItem) {
    if (!token) return;
    setPermissionMenu(menu);
    if (permission) {
      try {
        const latest = await backendClient.menuPermissions(token, menu.id);
        const current = latest.find((item) => item.id === permission.id) ?? permission;
        setEditingPermission(current);
        setPermissionForm({ permissionCode: current.permissionCode ?? current.authMark, permissionName: current.title, permissionType: current.permissionType ?? "action", authMark: current.authMark, sort: current.sort ?? 1, enabled: current.enabled ?? true });
      } catch { setEditingPermission(permission); setPermissionForm({ permissionCode: permission.permissionCode ?? permission.authMark, permissionName: permission.title, permissionType: permission.permissionType ?? "action", authMark: permission.authMark, sort: permission.sort ?? 1, enabled: permission.enabled ?? true }); }
    } else { setEditingPermission(null); setPermissionForm({ ...emptyPermission }); }
    setPermissionOpen(true);
  }

  async function savePermission() {
    if (!token || !permissionMenu) return;
    if (!permissionForm.permissionName.trim() || !permissionForm.permissionCode.trim()) return toast.error("请填写权限名称和权限编码");
    try { if (editingPermission) await backendClient.updateMenuPermission(token, permissionMenu.id, editingPermission.id, permissionForm); else await backendClient.createMenuPermission(token, permissionMenu.id, permissionForm); toast.success(editingPermission ? "权限修改成功" : "权限新增成功"); setPermissionOpen(false); menus.refresh(); }
    catch { toast.error("权限保存失败"); }
  }

  async function runConfirm() {
    if (!token || !confirm) return;
    try {
      if (confirm.type === "menu") await backendClient.deleteMenu(token, confirm.menu.id);
      else await backendClient.deleteMenuPermission(token, confirm.menu.id, confirm.permission.id);
      toast.success(confirm.type === "menu" ? "菜单已删除" : "权限已删除");
      setConfirm(null); menus.refresh();
    } catch { toast.error(confirm.type === "menu" ? "菜单删除失败" : "权限删除失败"); }
  }

  function toggleExpanded(id: number) { setExpanded((current) => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; }); }

  return (
    <div className="flex h-full min-h-0 w-full flex-col gap-4 p-4 md:p-6">
      <Card className="flex min-h-0 flex-1 flex-col border border-border">
        <CardHeader>
          <CardTitle>菜单列表</CardTitle>
          <CardAction>
            <Button onClick={() => openAdd()}><Plus className="size-4" />添加菜单</Button>
          </CardAction>
        </CardHeader>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3">
          {/* 由外层统一横纵滚动，避免 Table 内层 overflow 把底栏滚到表格最底部 */}
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border [&_[data-slot=table-container]]:overflow-visible">
            <Table className="min-w-[1280px]">
              <TableHeader className="sticky top-0 z-10 bg-muted/50">
                <TableRow>
                  <TableHead className="min-w-64 text-center">菜单名称</TableHead>
                  <TableHead className="min-w-20 text-center">类型</TableHead>
                  <TableHead className="min-w-44 text-center">路由</TableHead>
                  <TableHead className="min-w-52 text-center">组件</TableHead>
                  <TableHead className="min-w-16 text-center">排序</TableHead>
                  <TableHead className="min-w-20 text-center">状态</TableHead>
                  <TableHead className="min-w-72 text-center">按钮权限</TableHead>
                  <TableHead className="min-w-40 text-center">操作</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {menus.loading && Array.from({ length: 6 }, (_, index) => <TableRow key={index}><TableCell colSpan={8}><Skeleton className="h-8 w-full" /></TableCell></TableRow>)}
                {!menus.loading && rows.map(({ menu, depth }) => (
                  <TableRow key={menu.id}>
                    <TableCell>
                      <div className="flex items-center" style={{ paddingLeft: `${depth * 22}px` }}>
                        {menu.children?.length ? (
                          <Button size="icon" variant="ghost" className="mr-1 size-7" title={expanded.has(menu.id) ? "收起子菜单" : "展开子菜单"} onClick={() => toggleExpanded(menu.id)}>
                            {expanded.has(menu.id) ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}
                          </Button>
                        ) : (
                          <span className="mr-1 inline-block size-7" />
                        )}
                        <span className="font-medium">{menu.meta.title || menu.name}</span>
                      </div>
                    </TableCell>
                    <TableCell>
                      <Badge variant={menu.menuType === "directory" ? "secondary" : menu.menuType === "button" ? "destructive" : "default"}>
                        {menu.menuType === "directory" ? "目录" : menu.menuType === "button" ? "按钮" : "菜单"}
                      </Badge>
                    </TableCell>
                    <TableCell className="font-mono text-xs">{menu.path || "-"}</TableCell>
                    <TableCell className="font-mono text-xs">{menu.component || "-"}</TableCell>
                    <TableCell>{menu.sort ?? 1}</TableCell>
                    <TableCell>
                      <Badge variant={menu.enabled ? "outline" : "destructive"}>{menu.enabled ? "启用" : "禁用"}</Badge>
                    </TableCell>
                    <TableCell>
                      <div className="flex max-w-80 flex-wrap gap-1">
                        {(menu.meta.authList ?? []).map((permission) => (
                          <span key={permission.id} className="inline-flex items-center rounded-md border bg-muted/30">
                            <button type="button" className="px-2 py-1 text-xs" title="编辑权限" onClick={() => void openPermission(menu, permission)}>{permission.title || permission.authMark}</button>
                            <button type="button" className="border-l px-1.5 py-1 text-muted-foreground hover:text-destructive" title="删除权限" onClick={() => setConfirm({ type: "permission", menu, permission })}>×</button>
                          </span>
                        ))}
                        {!menu.meta.authList?.length && <span className="text-muted-foreground">-</span>}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <Button size="icon" variant="ghost" title="新增子菜单" onClick={() => openAdd(menu)}><Plus className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="新增按钮权限" onClick={() => void openPermission(menu)}><KeyRound className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="编辑菜单" onClick={() => openEdit(menu)}><Pencil className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="删除菜单" onClick={() => setConfirm({ type: "menu", menu })}><Trash2 className="size-4" /></Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!menus.loading && !rows.length && <TableRow><TableCell colSpan={8} className="h-32 text-center text-muted-foreground">{menus.error ?? "暂无菜单"}</TableCell></TableRow>}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

    <Dialog open={menuOpen} onOpenChange={setMenuOpen}><DialogContent className="max-h-[88vh] max-w-2xl overflow-y-auto"><DialogHeader><DialogTitle>{editingMenu ? "编辑菜单" : menuForm.parentId ? "新增子菜单" : "新增菜单"}</DialogTitle></DialogHeader><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2"><Label>菜单名称</Label><Input value={menuForm.name} onChange={(event) => setMenuForm((value) => ({ ...value, name: event.target.value }))} placeholder="如 DeviceMonitor" /></div><div className="space-y-2"><Label>标题</Label><Input value={menuForm.titleKey} onChange={(event) => setMenuForm((value) => ({ ...value, titleKey: event.target.value }))} /></div><div className="space-y-2"><Label>路由地址</Label><Input value={menuForm.path} onChange={(event) => setMenuForm((value) => ({ ...value, path: event.target.value }))} /></div><div className="space-y-2"><Label>组件路径</Label><Input value={menuForm.component} onChange={(event) => setMenuForm((value) => ({ ...value, component: event.target.value }))} /></div><div className="space-y-2"><Label>图标</Label><Input value={menuForm.icon ?? ""} onChange={(event) => setMenuForm((value) => ({ ...value, icon: event.target.value }))} /></div><div className="space-y-2"><Label>菜单类型</Label><Select value={menuForm.menuType ?? "menu"} onValueChange={(menuType) => setMenuForm((value) => ({ ...value, menuType }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="directory">目录</SelectItem><SelectItem value="menu">菜单</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>排序</Label><Input type="number" min={1} max={999} value={menuForm.sort ?? 1} onChange={(event) => setMenuForm((value) => ({ ...value, sort: Number(event.target.value) }))} /></div><div className="flex items-center justify-between rounded-md border p-3"><Label>状态</Label><Switch checked={menuForm.enabled} onCheckedChange={(enabled) => setMenuForm((value) => ({ ...value, enabled }))} /></div>{[["页面缓存", "keepAlive"], ["隐藏菜单", "isHide"], ["隐藏标签", "isHideTab"]].map(([label, key]) => <div key={key} className="flex items-center justify-between rounded-md border p-3"><Label>{label}</Label><Switch checked={Boolean(menuForm[key as keyof MenuPayload])} onCheckedChange={(checked) => setMenuForm((value) => ({ ...value, [key]: checked }))} /></div>)}</div><DialogFooter><Button variant="outline" onClick={() => setMenuOpen(false)}>取消</Button><Button onClick={() => void saveMenu()}>确定</Button></DialogFooter></DialogContent></Dialog>

    <Dialog open={permissionOpen} onOpenChange={setPermissionOpen}><DialogContent><DialogHeader><DialogTitle>{editingPermission ? "编辑权限" : "新增权限"}</DialogTitle></DialogHeader><div className="space-y-4"><div className="space-y-2"><Label>权限名称</Label><Input value={permissionForm.permissionName} onChange={(event) => setPermissionForm((value) => ({ ...value, permissionName: event.target.value }))} placeholder="如 新增、编辑、删除" /></div><div className="space-y-2"><Label>权限编码</Label><Input disabled={Boolean(editingPermission)} value={permissionForm.permissionCode} onChange={(event) => setPermissionForm((value) => ({ ...value, permissionCode: event.target.value }))} placeholder="如 system:user:create" /></div><div className="space-y-2"><Label>权限标识</Label><Input value={permissionForm.authMark ?? ""} onChange={(event) => setPermissionForm((value) => ({ ...value, authMark: event.target.value }))} /></div><div className="space-y-2"><Label>权限类型</Label><Select value={permissionForm.permissionType ?? "action"} onValueChange={(permissionType) => setPermissionForm((value) => ({ ...value, permissionType }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="action">按钮权限</SelectItem><SelectItem value="page">页面权限</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>排序</Label><Input type="number" min={1} max={999} value={permissionForm.sort ?? 1} onChange={(event) => setPermissionForm((value) => ({ ...value, sort: Number(event.target.value) }))} /></div><div className="flex items-center justify-between rounded-md border p-3"><Label>状态</Label><Switch checked={permissionForm.enabled} onCheckedChange={(enabled) => setPermissionForm((value) => ({ ...value, enabled }))} /></div></div><DialogFooter><Button variant="outline" onClick={() => setPermissionOpen(false)}>取消</Button><Button onClick={() => void savePermission()}>确定</Button></DialogFooter></DialogContent></Dialog>

    <AlertDialog open={Boolean(confirm)} onOpenChange={(open) => { if (!open) setConfirm(null); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirm?.type === "menu" ? "删除菜单" : "删除权限"}</AlertDialogTitle><AlertDialogDescription>{confirm?.type === "menu" ? `确定删除菜单“${confirm.menu.meta.title}”吗？包含的子菜单和权限可能一并删除。` : `确定删除权限“${confirm?.permission.title}”吗？`}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={() => void runConfirm()}>确认删除</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
    </div>
  );
}

export default MenuManagementPage;
