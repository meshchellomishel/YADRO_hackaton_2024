export BUS      ?= AHB
export CFG      ?= MAX

export ARCH         := IMC
export VECT_IRQ     := 1
export IPIC         := 1
export TCM          := 1
export SIM_CFG_DEF  := SCR1_CFG_RV32IMC_MAX

ARCH_lowercase = $(shell echo $(ARCH) | tr A-Z a-z)
BUS_lowercase  = $(shell echo $(BUS)  | tr A-Z a-z)

ifeq ($(ARCH_lowercase),)
    ARCH_tmp = imc
else
    ifneq (,$(findstring e,$(ARCH_lowercase)))
        ARCH_tmp   += e
        EXT_CFLAGS += -D__RVE_EXT
    else
        ARCH_tmp   += i
    endif
    ifneq (,$(findstring m,$(ARCH_lowercase)))
        ARCH_tmp   := $(ARCH_tmp)m
    endif
    ifneq (,$(findstring c,$(ARCH_lowercase)))
        ARCH_tmp   := $(ARCH_tmp)c
        EXT_CFLAGS += -D__RVC_EXT
    endif
    ifneq (,$(findstring b,$(ARCH_lowercase)))
        ARCH_tmp   := $(ARCH_tmp)b
    endif
endif

override ARCH=$(ARCH_tmp)

export XLEN  ?= 32
export ABI   ?= ilp32
