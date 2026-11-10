import { Router } from 'express';
import * as authService from '../services/authService';
import { validate, schemas } from '../middleware/validation';
import { authLimiter } from '../middleware/rateLimiter';
import { AppError, asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest } from '../types';

const router = Router();

router.post(
  '/register',
  authLimiter,
  validate(schemas.register),
  asyncHandler(async (req, res) => {
    const { email, password, displayName } = req.body;
    const { account, tokens } = await authService.register(displayName, email, password);

    res.status(201).json({
      status: 'success',
      data: {
        account: { id: account.id, displayName: account.display_name },
        tokens,
      },
    });
  })
);

router.post(
  '/login',
  authLimiter,
  validate(schemas.login),
  asyncHandler(async (req, res) => {
    const { email, password } = req.body;
    const { account, tokens } = await authService.login(email, password);

    res.json({
      status: 'success',
      data: {
        account: { id: account.id, displayName: account.display_name },
        tokens,
      },
    });
  })
);

router.post(
  '/platform/nintendo',
  authLimiter,
  validate(schemas.platformAuth),
  asyncHandler(async (req, res) => {
    const { platform, platformUserId, displayName } = req.body;
    const { account, tokens, created } = await authService.platformAuth(
      platform, platformUserId, displayName
    );

    res.status(created ? 201 : 200).json({
      status: 'success',
      data: {
        account: { id: account.id, displayName: account.display_name },
        tokens,
        created,
      },
    });
  })
);

router.post(
  '/mobile',
  authLimiter,
  validate(schemas.mobileAuth),
  asyncHandler(async (req, res) => {
    const { email, password, deviceType, deviceId, deviceName } = req.body;
    const { account, tokens } = await authService.login(email, password);

    res.json({
      status: 'success',
      data: {
        account: { id: account.id, displayName: account.display_name },
        tokens,
      },
    });
  })
);

router.post(
  '/refresh',
  validate(schemas.refreshToken),
  asyncHandler(async (req, res) => {
    const { refreshToken } = req.body;
    const tokens = await authService.refreshTokens(refreshToken);

    res.json({
      status: 'success',
      data: { tokens },
    });
  })
);

router.post(
  '/logout',
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const authHeader = req.headers.authorization;
    if (!authHeader) {
      throw new AppError(401, 'Not authenticated');
    }

    // Extract account from token for logout
    const jwt = require('jsonwebtoken');
    const config = require('../config').config;
    try {
      const token = authHeader.split(' ')[1];
      const decoded = jwt.verify(token, config.jwt.secret);
      await authService.logout(decoded.accountId, req.body?.refreshToken);
    } catch {
      // Token may be expired, that's fine for logout
    }

    res.json({ status: 'success', message: 'Logged out' });
  })
);

export default router;
