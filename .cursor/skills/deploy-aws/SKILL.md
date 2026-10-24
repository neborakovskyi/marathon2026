---
name: deploy-aws
description: >-
  Deploys and tears down the restaurant stack: Terraform shared infra, AWS Syndicate
  for Lambdas/API Gateway, Angular build synced to S3/CloudFront. Use when the user
  asks to deploy, update, destroy, or configure AWS/Syndicate/Terraform for this repo.
---

# Deploy AWS

Follow root `README.md` in order. Do not skip Terraform if the frontend bucket or CloudFront does not exist yet.

1. Terraform — `infra/terraform` (state bucket `restaurant-terraform-state-$ACCOUNT_ID`, region `us-west-2`)
2. Syndicate — `backend/restaurant-backend-app` with `SDCT_CONF` pointing at `.syndicate-config-restaurant-backend-app`
3. Frontend — set `BACKEND_URL` to `https://<api-id>.execute-api.us-west-2.amazonaws.com/develop`, `npm ci && npm run build`, `aws s3 sync` to `restaurant-frontend-$ACCOUNT_ID`

Syndicate details and IAM/bucket setup: `backend/restaurant-backend-app/local-setup-guide.md`.

Before deploy, the operator must have already replaced account id, email, `project_path`, and bucket names in:

- `infra/terraform/backend.tf`, `variables.tf`
- `.syndicate-config-restaurant-backend-app/syndicate.yml`
- `.syndicate-config-restaurant-backend-app/syndicate_aliases.yml`

Do not commit secrets or rewrite other people's account IDs unless they asked.

Destroy in reverse (empty frontend bucket → `syndicate clean` → `terraform destroy` → delete state and artifacts buckets). Commands are in the root README.

Incremental backend: `syndicate update`. Full: `syndicate build` then `syndicate deploy --deploy_name dev`.
