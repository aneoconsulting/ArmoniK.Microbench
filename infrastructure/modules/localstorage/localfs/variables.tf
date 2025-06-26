variable "storage_path" {
  description = "Path to a directory in the runner's local storage to use as object storage"
  type        = string
}

variable "config_file_path" {
  description = "Path to the local directory to use as the configuration path"
  type        = string
  default     = null
}

# NOTE: Should I add prefix, region and profile (maintain homogeneity?)
