/**
 * 简单用户认证（无外部依赖）
 */

import crypto from 'crypto'

interface User {
  username: string
  passwordHash: string
  role: 'admin' | 'engineer' | 'operator'
}

interface Session {
  token: string
  username: string
  role: string
  createdAt: number
}

let users: User[] = [
  // 默认管理员
  { username: 'admin', passwordHash: hashPassword('admin'), role: 'admin' },
  { username: 'operator', passwordHash: hashPassword('1234'), role: 'operator' },
]

const sessions = new Map<string, Session>()

function hashPassword(password: string): string {
  return crypto.createHash('sha256').update(password).digest('hex')
}

export function authenticate(username: string, password: string): { token: string; role: string } | null {
  const user = users.find(u => u.username === username && u.passwordHash === hashPassword(password))
  if (!user) return null
  const token = crypto.randomBytes(24).toString('hex')
  const session: Session = { token, username: user.username, role: user.role, createdAt: Date.now() }
  sessions.set(token, session)
  return { token, role: user.role }
}

export function validateToken(token: string): Session | null {
  const session = sessions.get(token)
  if (!session) return null
  // Session expires after 24h
  if (Date.now() - session.createdAt > 24 * 3600 * 1000) {
    sessions.delete(token)
    return null
  }
  return session
}

export function logout(token: string): void {
  sessions.delete(token)
}

export function getUsers(): { username: string; role: string }[] {
  return users.map(u => ({ username: u.username, role: u.role }))
}

export async function addUser(username: string, password: string, role: string): Promise<void> {
  if (users.find(u => u.username === username)) throw new Error('用户已存在')
  users.push({ username, passwordHash: hashPassword(password), role: role as any })
}

export async function removeUser(username: string): Promise<void> {
  users = users.filter(u => u.username !== username)
}

export async function changePassword(username: string, oldPassword: string, newPassword: string): Promise<boolean> {
  const user = users.find(u => u.username === username && u.passwordHash === hashPassword(oldPassword))
  if (!user) return false
  user.passwordHash = hashPassword(newPassword)
  return true
}

/** 从 Authorization header 提取 token */
export function extractToken(req: any): string | null {
  const auth = req.headers.authorization
  if (!auth || !auth.startsWith('Bearer ')) return null
  return auth.slice(7)
}
