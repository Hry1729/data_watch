FROM node:22-alpine AS builder

WORKDIR /app

ENV HUSKY=0
ENV PNPM_HOME=/pnpm
ENV PATH=$PNPM_HOME:$PATH

RUN corepack enable

COPY frontend/package.json frontend/pnpm-lock.yaml ./
RUN pnpm config set dangerouslyAllowAllBuilds true \
  && pnpm install --frozen-lockfile

COPY frontend/ .
# 「项目文档」模块用 Vite glob 在构建期内联仓库根 docs/ 的 markdown
# （见 src/views/project-docs/docs.config.ts 的 ../../../../docs glob）。
# 复刻本地相对结构：frontend 根在 /app，docs 必须是其同级目录 /docs。
COPY docs/ /docs
RUN pnpm build

FROM nginx:1.27-alpine

COPY --from=builder /app/dist /usr/share/nginx/html
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
