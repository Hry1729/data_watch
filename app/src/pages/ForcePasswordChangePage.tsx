import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuth } from "@/contexts/AuthContext";
import { backendClient } from "@/lib/backend-client";
import { passwordChangeCompletion } from "@/lib/password-change-flow";
import { toast } from "sonner";

export function ForcePasswordChangePage() {
  const { auth, logout } = useAuth();
  const [oldPassword, setOldPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!auth || loading) return;
    if (!oldPassword || newPassword.length < 6) {
      setError("请输入当前密码，新密码至少 6 位");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("两次输入的新密码不一致");
      return;
    }

    setLoading(true);
    setError(null);
    try {
      await backendClient.changePassword(auth.backendToken, oldPassword, newPassword);
      const completion = passwordChangeCompletion();
      if (completion.clearSession) {
        // Backend increments tokenVersion, so neither old token may be reused.
        toast.success("密码修改成功，请使用新密码重新登录");
        logout();
      }
    } catch {
      setError("密码修改失败，请确认当前密码并重试");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex h-full items-center justify-center bg-background px-4">
      <Card className="w-full max-w-md border border-border">
        <CardHeader>
          <CardTitle>请修改临时密码</CardTitle>
          <p className="text-sm text-muted-foreground">
            账号 {auth?.user?.userName || "当前用户"} 必须先设置新密码才能继续使用系统。
          </p>
        </CardHeader>
        <CardContent>
          <form onSubmit={submit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="force-change-old-password">当前密码</Label>
              <Input
                id="force-change-old-password"
                type="password"
                autoComplete="current-password"
                value={oldPassword}
                onChange={(event) => setOldPassword(event.target.value)}
                disabled={loading}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="force-change-new-password">新密码</Label>
              <Input
                id="force-change-new-password"
                type="password"
                autoComplete="new-password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                disabled={loading}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="force-change-confirm-password">确认新密码</Label>
              <Input
                id="force-change-confirm-password"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(event) => setConfirmPassword(event.target.value)}
                disabled={loading}
              />
            </div>
            {error && <p className="text-sm text-destructive">{error}</p>}
            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? "提交中…" : "保存新密码"}
            </Button>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

export default ForcePasswordChangePage;
