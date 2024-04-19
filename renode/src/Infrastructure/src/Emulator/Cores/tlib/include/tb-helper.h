#ifndef __TB_HELPER_H__
#define __TB_HELPER_H__

#include "tcg-op.h"

#include <infrastructure.h>

#include <global_helper.h>
#define GEN_HELPER 1
#include <global_helper.h>

#include "helper.h"
#define GEN_HELPER 1
#include "helper.h"

extern TCGv_ptr cpu_env;

#endif
