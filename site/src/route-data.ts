import { defineRouteMiddleware } from '@astrojs/starlight/route-data';

// Japanese pages get the Japanese social card.
export const onRequest = defineRouteMiddleware((context) => {
  const route = context.locals.starlightRoute;
  if (route.locale !== 'ja') return;
  const og = route.head.find((h) => h.tag === 'meta' && h.attrs?.property === 'og:image');
  if (og?.attrs) og.attrs.content = String(og.attrs.content).replace(/og\.png$/, 'og-ja.png');
});
