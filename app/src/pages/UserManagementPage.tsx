import { useCallback, useState } from "react";
import { toast } from "sonner";
import { KeyRound, Pencil, Plus, Search, Trash2 } from "lucide-react";
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
import { useAuth } from "@/contexts/AuthContext";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import { backendClient } from "@/lib/backend-client";
import type { UserListItem, UserPayload, UserQuery } from "@/types/system-management";

const emptyForm: UserPayload = { userName: "", nickName: "", userPhone: "", userEmail: "", userGender: "unknown", status: "1", userRoles: ["R_USER"] };
const genderName: Record<string, string> = { male: "男", female: "女", unknown: "未知" };

export function UserManagementPage() {
  const { auth } = useAuth();
  const token = auth?.backendToken;
  const [draft, setDraft] = useState<UserQuery>({ status: "1" });
  const [query, setQuery] = useState<UserQuery>({ current: 1, size: 20, status: "1" });
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<UserListItem | null>(null);
  const [form, setForm] = useState<UserPayload>(emptyForm);
  const [confirm, setConfirm] = useState<{ type: "delete" | "reset"; user: UserListItem } | null>(null);
  const [temporaryPassword, setTemporaryPassword] = useState<string | null>(null);

  const loadUsers = useCallback((activeToken: string, signal: AbortSignal) => backendClient.userList(activeToken, query, signal), [query]);
  const users = useBackendQuery(loadUsers);
  const loadRoles = useCallback((activeToken: string, signal: AbortSignal) => backendClient.roleOptions(activeToken, signal), []);
  const roles = useBackendQuery(loadRoles);
  const currentPage = query.current ?? 1;
  const pageSize = query.size ?? 20;
  const totalPages = Math.max(1, Math.ceil((users.data?.total ?? 0) / pageSize));

  function openCreate() {
    setEditing(null);
    setForm({ ...emptyForm, userRoles: ["R_USER"] });
    setDialogOpen(true);
  }

  function openEdit(user: UserListItem) {
    setEditing(user);
    setForm({ userName: user.userName, nickName: user.nickName, userPhone: user.userPhone, userEmail: user.userEmail, userGender: user.userGender, status: user.status, userRoles: [...(user.userRoles ?? [])] });
    setDialogOpen(true);
  }

  async function saveUser() {
    if (!token) return;
    if (form.userName.trim().length < 2) return toast.error("用户名至少需要 2 个字符");
    if (form.userEmail && !/^\S+@\S+\.\S+$/.test(form.userEmail)) return toast.error("请输入正确的邮箱格式");
    try {
      if (editing) {
        await backendClient.updateUser(token, editing.id, form);
      } else {
        const result = await backendClient.createUser(token, form);
        setTemporaryPassword(result.temporaryPassword);
      }
      toast.success(editing ? "用户更新成功" : "用户添加成功");
      setDialogOpen(false);
      users.refresh();
    } catch { toast.error("用户保存失败"); }
  }

  async function toggleStatus(user: UserListItem, enabled: boolean) {
    if (!token) return;
    try { await backendClient.updateUserStatus(token, user.id, enabled ? "1" : "2"); toast.success("账号状态已更新"); users.refresh(); }
    catch { toast.error("状态更新失败"); }
  }

  async function runConfirm() {
    if (!token || !confirm) return;
    try {
      if (confirm.type === "delete") { await backendClient.deleteUser(token, confirm.user.id); toast.success("用户已删除"); users.refresh(); }
      else {
        const result = await backendClient.resetUserPassword(token, confirm.user.id);
        setTemporaryPassword(result.temporaryPassword);
        toast.success("密码已重置，请通过安全渠道交付临时密码");
      }
      setConfirm(null);
    } catch { toast.error(confirm.type === "delete" ? "删除失败" : "重置密码失败"); }
  }

  function toggleRole(roleCode: string, checked: boolean) {
    setForm((current) => ({ ...current, userRoles: checked ? [...(current.userRoles ?? []), roleCode] : (current.userRoles ?? []).filter((code) => code !== roleCode) }));
  }

  return (
    <div className="flex h-full min-h-0 w-full flex-col">
      <Card className="flex min-h-0 flex-1 flex-col gap-0 py-0">
        <div className="flex flex-wrap items-center gap-2 px-4 pt-4">
          <Input className="h-7 w-44" value={draft.userName ?? ""} onChange={(event) => setDraft((value) => ({ ...value, userName: event.target.value }))} placeholder="用户名" />
          <Select value={draft.userGender || "all"} onValueChange={(value) => setDraft((current) => ({ ...current, userGender: value === "all" ? "" : value }))}>
            <SelectTrigger className="!h-7 w-32"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">性别（全部）</SelectItem>
              <SelectItem value="male">男</SelectItem>
              <SelectItem value="female">女</SelectItem>
              <SelectItem value="unknown">未知</SelectItem>
            </SelectContent>
          </Select>
          <Input className="h-7 w-40" value={draft.userPhone ?? ""} onChange={(event) => setDraft((value) => ({ ...value, userPhone: event.target.value }))} placeholder="手机号" />
          <Select value={draft.status || "all"} onValueChange={(value) => setDraft((current) => ({ ...current, status: value === "all" ? "" : value }))}>
            <SelectTrigger className="!h-7 w-36"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="all">账号状态（全部）</SelectItem>
              <SelectItem value="1">启用</SelectItem>
              <SelectItem value="2">禁用</SelectItem>
            </SelectContent>
          </Select>
          <div className="ml-auto flex flex-wrap items-center justify-end gap-2">
            <Button size="sm" variant="outline" onClick={() => { setDraft({}); setQuery({ current: 1, size: pageSize }); }}>重置</Button>
            <Button size="sm" variant="outline" onClick={() => setQuery({ ...draft, current: 1, size: pageSize })}><Search data-icon="inline-start" />查询</Button>
            <Button size="sm" onClick={openCreate}><Plus data-icon="inline-start" />新增用户</Button>
          </div>
        </div>
        <CardContent className="flex min-h-0 flex-1 flex-col gap-3 pt-3">
          <div className="min-h-0 flex-1 overflow-auto rounded-lg border">
            <Table>
              <TableHeader className="bg-muted/50">
                <TableRow>
                  <TableHead className="w-16 text-center">序号</TableHead>
                  <TableHead className="min-w-64 text-center">用户名</TableHead>
                  <TableHead className="text-center">昵称</TableHead>
                  <TableHead className="text-center">性别</TableHead>
                  <TableHead className="text-center">手机号</TableHead>
                  <TableHead className="text-center">角色</TableHead>
                  <TableHead className="text-center">账号状态</TableHead>
                  <TableHead className="text-center">创建日期</TableHead>
                  <TableHead className="text-center">操作</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.loading && Array.from({ length: 6 }, (_, index) => <TableRow key={index}><TableCell colSpan={9}><Skeleton className="h-8 w-full" /></TableCell></TableRow>)}
                {!users.loading && (users.data?.records ?? []).map((user, index) => (
                  <TableRow key={user.id}>
                    <TableCell>{(currentPage - 1) * pageSize + index + 1}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-3">
                        <div className="flex size-9 shrink-0 items-center justify-center rounded-md bg-primary/10 font-semibold text-primary">{(user.userName || "?").slice(0, 2).toUpperCase()}</div>
                        <p className="font-medium">{user.userName}</p>
                      </div>
                    </TableCell>
                    <TableCell>{user.nickName || "-"}</TableCell>
                    <TableCell>{genderName[user.userGender] ?? "未知"}</TableCell>
                    <TableCell>{user.userPhone || "-"}</TableCell>
                    <TableCell>
                      <div className="flex max-w-56 flex-wrap gap-1">{(user.userRoles ?? []).map((role) => <Badge key={role} variant="outline">{role}</Badge>)}</div>
                    </TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <Switch checked={user.status === "1"} onCheckedChange={(checked) => void toggleStatus(user, checked)} />
                        <span className="text-xs text-muted-foreground">{user.status === "1" ? "启用" : "禁用"}</span>
                      </div>
                    </TableCell>
                    <TableCell className="whitespace-nowrap">{user.createTime?.slice(0, 19)}</TableCell>
                    <TableCell>
                      <div className="flex gap-1">
                        <Button size="icon" variant="ghost" title="编辑用户" onClick={() => openEdit(user)}><Pencil className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="重置密码" onClick={() => setConfirm({ type: "reset", user })}><KeyRound className="size-4" /></Button>
                        <Button size="icon" variant="ghost" title="删除用户" disabled={user.userName === "admin"} onClick={() => setConfirm({ type: "delete", user })}><Trash2 className="size-4" /></Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
                {!users.loading && !users.data?.records?.length && <TableRow><TableCell colSpan={9} className="h-32 text-center text-muted-foreground">{users.error ?? "暂无用户"}</TableCell></TableRow>}
              </TableBody>
            </Table>
          </div>
          <div className="flex flex-col gap-3 pt-1 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
            <span>共 {users.data?.total ?? 0} 条</span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="outline" disabled={currentPage <= 1} onClick={() => setQuery((value) => ({ ...value, current: currentPage - 1 }))}>上一页</Button>
              <span>第 {currentPage} / {totalPages} 页</span>
              <Button size="sm" variant="outline" disabled={currentPage >= totalPages} onClick={() => setQuery((value) => ({ ...value, current: currentPage + 1 }))}>下一页</Button>
            </div>
          </div>
        </CardContent>
      </Card>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}><DialogContent><DialogHeader><DialogTitle>{editing ? "编辑用户" : "添加用户"}</DialogTitle></DialogHeader><div className="grid gap-4 sm:grid-cols-2"><div className="space-y-2 sm:col-span-2"><Label>用户名</Label><Input disabled={Boolean(editing)} value={form.userName} onChange={(event) => setForm((value) => ({ ...value, userName: event.target.value }))} /></div>{!editing && <p className="text-xs text-muted-foreground sm:col-span-2">创建后系统会生成一次性临时密码，提交成功后仅显示一次。</p>}<div className="space-y-2"><Label>昵称</Label><Input value={form.nickName ?? ""} onChange={(event) => setForm((value) => ({ ...value, nickName: event.target.value }))} /></div><div className="space-y-2"><Label>手机号</Label><Input value={form.userPhone ?? ""} onChange={(event) => setForm((value) => ({ ...value, userPhone: event.target.value }))} /></div><div className="space-y-2"><Label>邮箱</Label><Input value={form.userEmail ?? ""} onChange={(event) => setForm((value) => ({ ...value, userEmail: event.target.value }))} /></div><div className="space-y-2"><Label>性别</Label><Select value={form.userGender ?? "unknown"} onValueChange={(userGender) => setForm((value) => ({ ...value, userGender }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="unknown">未知</SelectItem><SelectItem value="male">男</SelectItem><SelectItem value="female">女</SelectItem></SelectContent></Select></div><div className="space-y-2"><Label>状态</Label><Select value={form.status ?? "1"} onValueChange={(status) => setForm((value) => ({ ...value, status }))}><SelectTrigger><SelectValue /></SelectTrigger><SelectContent><SelectItem value="1">启用</SelectItem><SelectItem value="2">禁用</SelectItem></SelectContent></Select></div><div className="space-y-2 sm:col-span-2"><Label>角色</Label><div className="grid max-h-40 gap-2 overflow-y-auto rounded-md border p-3 sm:grid-cols-2">{(roles.data ?? []).map((role) => <label key={role.roleCode} className="flex items-center gap-2 text-sm"><Checkbox disabled={!role.enabled} checked={(form.userRoles ?? []).includes(role.roleCode)} onCheckedChange={(checked) => toggleRole(role.roleCode, checked === true)} /><span>{role.roleName}</span><span className="text-xs text-muted-foreground">{role.roleCode}</span></label>)}</div></div></div><DialogFooter><Button variant="outline" onClick={() => setDialogOpen(false)}>取消</Button><Button onClick={() => void saveUser()}>提交</Button></DialogFooter></DialogContent></Dialog>

      <AlertDialog open={Boolean(confirm)} onOpenChange={(open) => { if (!open) setConfirm(null); }}><AlertDialogContent><AlertDialogHeader><AlertDialogTitle>{confirm?.type === "delete" ? "删除用户" : "重置密码"}</AlertDialogTitle><AlertDialogDescription>{confirm?.type === "delete" ? `确定要删除用户“${confirm.user.userName}”吗？此操作不可撤销。` : `确定为用户“${confirm?.user.userName}”生成一次性临时密码吗？用户首次登录后必须修改。`}</AlertDialogDescription></AlertDialogHeader><AlertDialogFooter><AlertDialogCancel>取消</AlertDialogCancel><AlertDialogAction onClick={() => void runConfirm()}>{confirm?.type === "delete" ? "确认删除" : "确认重置"}</AlertDialogAction></AlertDialogFooter></AlertDialogContent></AlertDialog>
      <Dialog open={temporaryPassword !== null} onOpenChange={(open) => { if (!open) setTemporaryPassword(null); }}><DialogContent><DialogHeader><DialogTitle>临时密码（仅显示本次）</DialogTitle></DialogHeader><p className="text-sm text-muted-foreground">请通过安全渠道交付给用户，关闭后不会再次显示，也不会保存到浏览器。</p><div className="rounded-md border bg-muted/40 p-3 text-center font-mono text-base select-all">{temporaryPassword}</div><DialogFooter><Button onClick={() => setTemporaryPassword(null)}>我已安全交付</Button></DialogFooter></DialogContent></Dialog>
    </div>
  );
}

export default UserManagementPage;
