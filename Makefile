# Environment
export CROSS_PREFIX  ?= /home/developer/sc-dt/riscv-gcc/bin/riscv64-unknown-elf-
export RISCV_GCC     ?= $(CROSS_PREFIX)gcc
export RISCV_OBJDUMP ?= $(CROSS_PREFIX)objdump -D
export RISCV_OBJCOPY ?= $(CROSS_PREFIX)objcopy -O verilog
export RISCV_READELF ?= $(CROSS_PREFIX)readelf -s

# Paths
export root_dir := $(shell pwd)
export tst_dir  := $(root_dir)/tests
export inc_dir  := $(root_dir)/common
export bld_dir  := $(root_dir)/build

#verilator and apb_uart rtl
export VERILATED_PATH ?= $(root_dir)/apb_uart
export VERILATOR ?= /home/developer/verilator
export verilated_bld ?= $(VERILATED_PATH)/build
export VERILATED_EXEC ?= $(verilated_bld)/verilated

# Renode
export RENODE := $(root_dir)/renode
export RENODE_YADRO_SCRIPTS ?= $(root_dir)
export RENODE_SCRIPT ?= yadro.resc
export RENODE_PLATFORM_DESCRIPTION ?= yadro.repl
export RENODE_MACHINE_NAME ?= yadro
export SYSBUS_MODULE ?= sysbus.cosim
export DEFUALT_TIMEOUT ?= 90s

# Export arch variables
include $(inc_dir)/arch.mk

# Use this parameter to set test to build
export TARGET ?= apb_uart_example

# Targets
.PHONY: build_test compile_verilator build_verilator ren-run


# List of available tests
build_test: $(TARGET)

$(TARGET): | $(bld_dir)
	-$(MAKE) -C $(tst_dir)/$@

$(bld_dir):
	mkdir -p $(bld_dir)

run_robot: compile_verilator $(TARGET)
	$(RENODE)/renode-test \
	    --show-log --verbose --debug-on-error --stop-on-error \
	    --variable ELF_FILE:$(bld_dir)/$(TARGET).elf \
	    --variable PLATFORM_DESC:$(RENODE_YADRO_SCRIPTS)/$(RENODE_PLATFORM_DESCRIPTION) \
	    --variable SYSBUS_MODULE:$(SYSBUS_MODULE) \
	    --variable SIMULATION_SCRIPT:$(VERILATED_EXEC) \
		--variable DEFUALT_TIMEOUT:$(DEFUALT_TIMEOUT)  \
	    $(root_dir)/yadro.robot && \
	mv $(root_dir)/logs/coverage.dat $(root_dir)/logs/$(TARGET).dat

coverage:
	cd $(root_dir)/logs && \
	verilator_coverage *.dat --write-info coverage.info && \
	genhtml coverage.info

compile_verilator: build_verilator
	cd $(verilated_bld) && \
	cmake $(VERILATED_PATH) -DUSER_RENODE_DIR=$(RENODE)/src/Plugins/VerilatorPlugin -DUSER_VERILATOR_DIR=$(VERILATOR)  && \
	make

TESTS := $(notdir $(wildcard tests/*))
compile_all_tests:
	@for test_case in $(TESTS); do \
		make TARGET=$${test_case}; \
	done 

prepare_robot:
	echo """\
	*** Variables ***\n\
	\$${ELF_FILE}                         none.elf\n\
	\$${PLATFORM_DESC}                    none.repl\n\
	\$${SYSBUS_MODULE}                    sysbus.none\n\
	\$${SIMULATION_SCRIPT}                none.sh\n\
	\$${LOG_TIMEOUT}                      1\n\
	\$${DEFUALT_TIMEOUT}                  10s\n\
	\n*** Keywords ***\n\
	Create Machine With Socket Based Communication\n\
		Execute Command                             using sysbus\n\
		Execute Command                             mach create "yadro"\n\
	\n*** Test Cases ***\n\
	# Starting emulation\
	""" > all_tests.robot &&\
	for test_case in $(TESTS); do \
		echo "$${test_case}" >> all_tests.robot; \
		echo -n "\t[Documentation]\t\t" >> all_tests.robot; \
		echo $$(cat tests/$${test_case}/$${test_case}.txt) >> all_tests.robot; \
		echo "\tExecute Command                 mach create \"yadro\"" >> all_tests.robot; \
		echo "\tExecute Command                 machine LoadPlatformDescription @\$${PLATFORM_DESC}" >> all_tests.robot; \
		echo "\t\$${stdout}=  Execute Command     \$${SYSBUS_MODULE} ConnectionParameters" >> all_tests.robot; \
		echo "\t@{words} =  Split String    \$${stdout}       \$${SPACE}" >> all_tests.robot; \
		echo "\tLog To Console  ${words}[0]\n\tLog To Console  ${words}[1]" >> all_tests.robot; \
		echo "\t\$${proc}=    Start process   \$${SIMULATION_SCRIPT}      \$${words}[0]      \$${words}[1]     shell=True" >> all_tests.robot; \
		echo "\tSleep   2s" >> all_tests.robot; \
		echo "\tExecute Command                 \$${SYSBUS_MODULE} Connect" >> all_tests.robot; \
		echo "\tExecute Command                 sysbus LoadELF @$(root_dir)/build/$${test_case}.elf" >> all_tests.robot; \
		echo "\tStart Emulation" >> all_tests.robot; \
		echo "\t\$${result}=  Wait For Process    \$${proc}    timeout=\$${DEFUALT_TIMEOUT}\n" >> all_tests.robot; \
	done 

run_all: prepare_robot compile_verilator compile_all_tests
	$(RENODE)/renode-test \
	    --show-log --verbose --debug-on-error --stop-on-error \
	    --variable ELF_FILE:$(bld_dir)/$(TARGET).elf \
	    --variable PLATFORM_DESC:$(RENODE_YADRO_SCRIPTS)/$(RENODE_PLATFORM_DESCRIPTION) \
	    --variable SYSBUS_MODULE:$(SYSBUS_MODULE) \
	    --variable SIMULATION_SCRIPT:$(VERILATED_EXEC) \
		--variable DEFUALT_TIMEOUT:$(DEFUALT_TIMEOUT)  \
	    $(root_dir)/all_tests.robot && \
	mv $(root_dir)/logs/coverage.dat $(root_dir)/logs/all_tests.dat

# build rtl model
build_verilator:
	mkdir -p $(verilated_bld)

clean_verilator:
	rm -rf $(verilated_bld)

clean_test: $(TARGET)
	$(RM) -R $(root_dir)/build/$(TARGET)*

clean_all_test: $(TARGET)
	$(RM) -R $(root_dir)/build/*
