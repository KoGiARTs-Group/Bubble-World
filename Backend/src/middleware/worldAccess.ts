import { Response, NextFunction } from 'express';
import { db } from '../db';
import { AppError } from './errorHandler';
import { AuthenticatedRequest, param } from '../types';

export function requireWorldAccess(role: 'owner' | 'member' | 'viewer' = 'viewer') {
  return async (req: AuthenticatedRequest, res: Response, next: NextFunction): Promise<void> => {
    const worldId = param(req, 'worldId');
    if (!worldId) {
      throw new AppError(400, 'Missing worldId parameter');
    }

    if (!req.accountId) {
      throw new AppError(401, 'Not authenticated');
    }

    const world = await db.query(
      'SELECT owner_account_id FROM worlds WHERE id = $1',
      [worldId]
    );

    if (world.rows.length === 0) {
      throw new AppError(404, 'World not found');
    }

    if (world.rows[0].owner_account_id === req.accountId) {
      next();
      return;
    }

    const member = await db.query(
      'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
      [worldId, req.accountId]
    );

    if (member.rows.length === 0) {
      throw new AppError(403, 'Not authorized for this world');
    }

    const roleHierarchy: Record<string, number> = { viewer: 0, member: 1, owner: 2 };
    const userRole = member.rows[0].role as string;
    if ((roleHierarchy[userRole] || 0) < (roleHierarchy[role] || 0)) {
      throw new AppError(403, `Insufficient permissions. Required: ${role}, have: ${userRole}`);
    }

    next();
  };
}
