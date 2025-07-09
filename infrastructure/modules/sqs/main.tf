locals {
  config_file_path = coalesce(var.config_file_path, "${path.root}/benchmark_configs/sqs.json")
  service_url = "https://sqs.${var.region}.amazonaws.com"
  account_id  = data.aws_caller_identity.current.account_id

  tags = merge(var.additional_tags, {
    Module = "SQS"
  })
}

data "aws_caller_identity" "current" {}

data "aws_iam_policy_document" "sqs_permissions" {
  statement {
    sid = "SqsAdmin"
    actions = [
      "sqs:CreateQueue",
      "sqs:DeleteQueue",
      "sqs:ListQueues",
      "sqs:GetQueueAttributes",
      "sqs:PurgeQueue",
      "sqs:GetQueueUrl",
      "sqs:ReceiveMessage",
      "sqs:SendMessage",
      "sqs:DeleteMessage",
      "sqs:ChangeMessageVisibility"
    ]
    effect    = "Allow"
    resources = ["arn:aws:sqs:${var.region}:${local.account_id}:${var.prefix}*"]
  }
}

resource "aws_iam_policy" "sqs_policy" {
  name        = "${var.prefix}-sqs-admin-policy"
  description = "IAM policy for SQS administration for EC2 instances"
  policy      = data.aws_iam_policy_document.sqs_permissions.json
}

resource "aws_iam_role_policy_attachment" "sqs_policy_attachment" {
  role       = var.benchmark_runner_role_id
  policy_arn = aws_iam_policy.sqs_policy.arn
}
