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
{{- $_ := required "image.repository is required" .Values.image.repository -}}
{{- $_ := required "image.tag is required (use immutable release artifacts)" .Values.image.tag -}}
{{- $_ := required "application.connectionSecret is required" .Values.application.connectionSecret -}}
{{- if .Values.migration.enabled -}}
{{- $_ := required "migration.image.repository is required" .Values.migration.image.repository -}}
{{- $_ := required "migration.image.tag is required" .Values.migration.image.tag -}}
{{- $_ := required "migration.connectionSecret is required" .Values.migration.connectionSecret -}}
{{- if eq .Values.application.connectionSecret .Values.migration.connectionSecret -}}
{{- fail "API and migrator require separate connection secrets" -}}
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
