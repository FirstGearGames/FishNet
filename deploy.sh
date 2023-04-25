#!/bin/sh -ex

if test $# -eq 0
then
    set -- -h
fi

OPTS_SPEC="\
deploy.sh   --semver  <semver>\n\
--\n\
s,semver    Semantic versioning: 1.2.0, 1.2.0-preview.1\n\
h,help      https://semver.org/
"


while [[ "$#" -gt 0 ]]; do
    case $1 in
        -s|--semver)
            SEMVER="$2";
            shift
            ;;
        -h|--help)
            echo -e "$OPTS_SPEC";
            exit 0
            ;;
        *)
            echo "Unknown parameter passed: $1";
            exit 1
            ;;
    esac
    shift
done

[ -z "$SEMVER" ] && echo "You must provide the --semver option." && exit 1

PREFIX="Assets/FishNet"
BRANCH="upm"

git subtree split --prefix="$PREFIX" --branch $BRANCH
git tag $SEMVER $BRANCH
git push origin $BRANCH --tags
git push origin --delete $BRANCH
git branch -D $BRANCH

exit 0