import { Response, NextFunction } from 'express';
import jwt from 'jsonwebtoken';
import { config } from '../config';
import { AppError } from './errorHandler';
import { AuthenticatedRequest } from '../types';

export function requireAuth(req: AuthenticatedRequest, res: Response, next: NextFunction): void {
  const authHeader = req.headers.authorization;
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    throw new AppError(401, 'Missing or invalid authorization header');
  }

  const token = authHeader.split(' ')[1];
  try {
    const decoded = jwt.verify(token, config.jwt.secret) as {
      accountId: string;
      deviceId?: string;
    };
    req.accountId = decoded.accountId;
    req.deviceId = decoded.deviceId;
    req.player = {
      accountId: decoded.accountId,
      displayName: '',
    };
    next();
  } catch {
    throw new AppError(401, 'Invalid or expired token');
  }
}
