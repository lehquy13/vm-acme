{{- define "vm-acme.fullname" -}}
{{- .Values.fullnameOverride | default .Release.Name | trunc 50 | trimSuffix "-" -}}
{{- end -}}
{{- define "vm-acme.labels" -}}
app.kubernetes.io/name: vm-acme
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
{{- define "vm-acme.validate" -}}
{{- if not (has .Values.phase (list "application" "prerequisites")) -}}
{{- fail "phase must be application or prerequisites" -}}
{{- end -}}
{{- if eq .Values.application.serviceAccount .Values.migration.serviceAccount -}}
{{- fail "API and migrator require different service accounts" -}}
{{- end -}}
{{- if not (has .Values.persistence.provider (list "PostgreSql" "SqlServer")) -}}
{{- fail "persistence.provider must be PostgreSql or SqlServer" -}}
{{- end -}}
{{- if eq .Values.phase "application" -}}
{{- if not (has .Values.secrets.mode (list "Environment" "KeyVault")) -}}
{{- fail "secrets.mode must be Environment or KeyVault" -}}
{{- end -}}
{{- if and (eq .Values.profile "dev") (ne .Values.secrets.mode "KeyVault") -}}
{{- fail "dev profile requires KeyVault secret mode" -}}
{{- end -}}
{{- $_ := required "image.repository is required" .Values.image.repository -}}
{{- $_ := required "image.tag is required (use immutable release artifacts)" .Values.image.tag -}}
{{- if eq .Values.secrets.mode "KeyVault" -}}
{{- if ne (toString (index .Values.application.podLabels "azure.workload.identity/use")) "true" -}}
{{- fail "KeyVault requires application.podLabels azure.workload.identity/use=true" -}}
{{- end -}}
{{- $_ := required "KeyVault requires API workload identity client-id annotation" (index .Values.application.serviceAccountAnnotations "azure.workload.identity/client-id") -}}
{{- $_ := required "secrets.vaultUri is required" .Values.secrets.vaultUri -}}
{{- $_ := required "application.vaultConnectionSecret is required" .Values.application.vaultConnectionSecret -}}
{{- $_ := required "application.vaultJwtSecret is required" .Values.application.vaultJwtSecret -}}
{{- else -}}
{{- $_ := required "application.connectionSecret is required" .Values.application.connectionSecret -}}
{{- end -}}
{{- if .Values.migration.enabled -}}
{{- if not (has .Values.migration.artifact (list "Runner" "Bundle")) -}}
{{- fail "migration.artifact must be Runner or Bundle" -}}
{{- end -}}
{{- if and (eq .Values.profile "dev") (ne .Values.migration.artifact "Bundle") -}}
{{- fail "dev profile requires a provider-specific self-contained Bundle image" -}}
{{- end -}}
{{- if and (eq .Values.migration.artifact "Bundle") (or .Values.migration.target .Values.migration.failForPoc) -}}
{{- fail "Bundle mode does not support runner migration.target or failure injection" -}}
{{- end -}}
{{- $_ := required "migration.image.repository is required" .Values.migration.image.repository -}}
{{- $_ := required "migration.image.tag is required" .Values.migration.image.tag -}}
{{- if eq .Values.secrets.mode "KeyVault" -}}
{{- if ne (toString (index .Values.migration.podLabels "azure.workload.identity/use")) "true" -}}
{{- fail "KeyVault requires migration.podLabels azure.workload.identity/use=true" -}}
{{- end -}}
{{- $_ := required "KeyVault requires migrator workload identity client-id annotation" (index .Values.migration.serviceAccountAnnotations "azure.workload.identity/client-id") -}}
{{- if eq (index .Values.application.serviceAccountAnnotations "azure.workload.identity/client-id") (index .Values.migration.serviceAccountAnnotations "azure.workload.identity/client-id") -}}
{{- fail "KeyVault requires distinct API/migrator client IDs" -}}
{{- end -}}
{{- $_ := required "migration.vaultConnectionSecret is required" .Values.migration.vaultConnectionSecret -}}
{{- if eq .Values.application.vaultConnectionSecret .Values.migration.vaultConnectionSecret -}}
{{- fail "API and migrator require separate vault connection secrets" -}}
{{- end -}}
{{- else -}}
{{- $_ := required "migration.connectionSecret is required" .Values.migration.connectionSecret -}}
{{- if eq .Values.application.connectionSecret .Values.migration.connectionSecret -}}
{{- fail "API and migrator require separate connection secrets" -}}
{{- end -}}
{{- end -}}
{{- if not (has .Values.migration.orchestration (list "helm" "argocd")) -}}
{{- fail "migration.orchestration must be helm or argocd" -}}
{{- end -}}
{{- if and .Values.migration.failForPoc (ne .Values.profile "poc") -}}
{{- fail "failure injection is allowed only in the poc profile" -}}
{{- end -}}
{{- end -}}
{{- if .Values.ingress.enabled -}}
{{- $_ := required "ingress.host is required" .Values.ingress.host -}}
{{- end -}}
{{- end -}}
{{- end -}}
