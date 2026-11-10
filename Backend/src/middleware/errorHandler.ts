import { Request, Response, NextFunction } from 'express';
import { config } from '../config';

export class AppError extends Error {
  public readonly statusCode: number;
  public readonly isOperational: boolean;
  public readonly details?: unknown;

  constructor(statusCode: number, message: string, isOperational = true, details?: unknown) {
    super(message);
    this.statusCode = statusCode;
    this.isOperational = isOperational;
    this.details = details;
    Object.setPrototypeOf(this, AppError.prototype);
  }
}

export function notFound(req: Request, _res: Response, next: NextFunction): void {
  next(new AppError(404, `Not found: ${req.method} ${req.originalUrl}`));
}

export function errorHandler(err: Error, _req: Request, res: Response, _next: NextFunction): void {
  if (err instanceof AppError) {
    const response: Record<string, unknown> = {
      status: 'error',
      message: err.message,
    };
    if (err.details) response.details = err.details;
    if (config.isDev) response.stack = err.stack;

    res.status(err.statusCode).json(response);
    return;
  }

  console.error('Unhandled error:', err);
  res.status(500).json({
    status: 'error',
    message: 'Internal server error',
  });
}

export function asyncHandler(
  fn: (req: Request, res: Response, next: NextFunction) => Promise<unknown>
) {
  return (req: Request, res: Response, next: NextFunction) => {
    Promise.resolve(fn(req, res, next)).catch(next);
  };
}
