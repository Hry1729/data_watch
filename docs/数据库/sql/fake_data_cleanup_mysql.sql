-- Executable MySQL cleanup script for STEP-007
USE device_watch;
DELETE FROM device_variable WHERE id IN (992001, 992002, 992003, 992004, 992005, 992006);
DELETE FROM device WHERE id IN (991001, 991002, 991003);
DELETE FROM variable_group WHERE id IN (9930, 9931, 9932);
DELETE FROM device_group WHERE id IN (9910, 9911, 9912);
DELETE FROM device_type WHERE id IN (9920, 9921, 9922);
DELETE FROM comm_protocol WHERE id IN (9901, 9902, 9903);
