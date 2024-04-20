TEST_NAME := test_reg

src_dir := $(dir $(lastword $(MAKEFILE_LIST)))

# Сюда пишем исходники теста и файлы которые он использует
c_src := sc_print.c $(TEST_NAME).c

include $(inc_dir)/common.mk

default: log_requested_tgt $(bld_dir)/$(TEST_NAME).elf $(bld_dir)/$(TEST_NAME).hex $(bld_dir)/$(TEST_NAME).dump

log_requested_tgt:
	echo $(TEST_NAME).hex>> $(bld_dir)/$(TEST_NAME)_test_info

clean:
	$(RM) $(c_objs) $(asm_objs) $(bld_dir)/$(TEST_NAME).elf $(bld_dir)/$(TEST_NAME).hex $(bld_dir)/$(TEST_NAME).dump
