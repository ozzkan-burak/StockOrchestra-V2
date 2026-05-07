{{/*
Expand the name of the chart.
*/}}
{{- define "stockorchestra.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "stockorchestra.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "stockorchestra.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{/*
Common labels
*/}}
{{- define "stockorchestra.labels" -}}
helm.sh/chart: {{ include "stockorchestra.chart" . }}
{{ include "stockorchestra.fullname" . }}
heritage: {{ .Release.Service }}
{{- end -}}

{{/*
Image full path
*/}}
{{- define "stockorchestra.image" -}}
{{- $registry := .Values.global.imageRegistry -}}
{{- $image := .image -}}
{{- $tag := .imageTag | default "latest" -}}
{{- printf "%s/%s:%s" $registry $image $tag -}}
{{- end -}}