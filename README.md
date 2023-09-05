# aws-auth reloader (aws-auth operator)

In EKS, managed K8S in AWS, there is a good mechanism to allow users to connect to cluster. IAM roles in AWS account are mapped to roles inside K8S cluster. But there is no mechanism to create and delete users manually based on some rules - all changes should be done manually. Until now.
This "operator" takes user list from AWS IAM and, after some filtering, applies it to aws-auth configmap. In a loop. You can deploy this in cluster and forget about "I forget to give it access!" or "I make a typo in configmap and lost cluster access" forever.
NB: Novadays a lot of DevOps engineers think that it's ok to use Terraform to manage something inside K8S. I don't share this point of view. I think that clusters can handle themselves.

## Build

Simply run 
```bash
docker build -t <some-tag> .
```

## Usage

Just put tags on your IAM entities! Like this:
|Tag name|Tag value|
|--------|---------|
|cluster:test-cluster|system:masters|
|cluster:our-stage|developers admins|
|cluster:production|readonly|

This means that in test-cluster this IAM entity will be with system:masters group, in "our-stage" will be two groups (developera and admins, separated by space) and production will be readonly group. Prefix for tag name can be changed, see table below.
Oh, yes, groups should be created by separated process. This operator only applies them, not creates.

## Configuration

Chart are accepting following configuration variables:
| Key | Type | Mandatory | Default | Description |
|-----|------|-----------|---------|-------------|
|LOG_LEVEL|string|no|info|Log level for operator. Can be debug,info,warn,error,fatal,off|
|AWS_REGION|string|yes||AWS region of working code. Should be setted like in aws profile, e.g. us-west-1|
|AWS_ACCESS_KEY_ID|string|no||Set this with creds to access AWS IAM (can be null if you are using role-based auth)|
|AWS_SECRET_ACCESS_KEY|string|no||Same as above|
|TAG_PREFIX|string|no|cluster:|Prefix to filter tags on IAM entities|
|CLUSTER_NAME|string|yes||Name of cluster for which operator should to search tags|
|DRY_RUN|string/bool|no|true|Fuse. Should be explicitely set to false because of aws-auth importance|
|REFRESH_INTERVAL|int|no|900|How frequently operator runs it's look. Mtasured in seconds|
|PROTECTED_ENTITIES|list|no||See below|

## Protected entities

To avoid modification and/or deletion of some entities that are crucial to cluster health here is another fuse: ARNs from this list will be removed from lists "to delete" and "to modify". So even if the worst case (i.e. tags wipe) this entities will stay in configmap. So you can store here role for nodes or backup IAM account to restore cluster liveness. Yes, I prefer to have more insurance about this.

## Helm chart

Currently it can be found in helm/ directory. There are another fuse for mandatory variables - chart install will fail if they stays empty. There will be repo for chart someday.