

USE ACCUNT_DB;
CREATE TABLE account_info_table (
user_index int NOT NULL AUTO_INCREMENT,
create_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
account_type tinyint NOT NULL,
last_login_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
PRIMARY KEY(user_index)
);

CREATE TABLE social_info_table (
  social_type tinyint,
  social_id varchar(64),
  email varchar(128),
  user_index int,
  PRIMARY KEY(social_type, social_id),
  KEY (user_index)
);

CREATE TABLE push_token_info_table (
  push_token varchar(128),
  os_type tinyint,
  user_index int,
  PRIMARY KEY(push_token, os_type),
  KEY (user_index)
);

CREATE TABLE daily_login_user_table (
  login_datetime DATE NOT NULL,
  user_index int NOT NULL,
  PRIMARY KEY (user_index,login_datetime),
  KEY (login_datetime)
);

#SELECT CURDATE();



DROP procedure IF EXISTS `sp_login`;


DELIMITER $$
USE `account_db`$$
CREATE DEFINER=`root`@`localhost` PROCEDURE `sp_login`(
	IN p_social_type int,
    IN p_social_id varchar(64),
	IN p_email varchar(128),
	IN p_push_token varchar(256),
    OUT o_success TINYINT,
	OUT o_error_code CHAR(5),
	OUT o_error_msg TEXT
    )
proc : BEGIN
	DECLARE v_errno INT DEFAULT 0;
	DECLARE v_sqlstate CHAR(5) DEFAULT '00000';
	DECLARE v_msg TEXT;

	DECLARE EXIT HANDLER FOR SQLEXCEPTION
	BEGIN
		-- 8.0.16+에서만 동작
		GET DIAGNOSTICS CONDITION 1
		v_errno = MYSQL_ERRNO,
		v_sqlstate = RETURNED_SQLSTATE,
		v_msg = MESSAGE_TEXT;
		ROLLBACK;

		SET o_success = 0;
		SET o_error_code = v_sqlstate;
		SET o_error_msg = CONCAT('errno=', v_errno, ', ', v_msg);
		-- 선택: 에러 로그 적재
		-- INSERT INTO error_log(proc_name, errno, sqlstate, message, created_at)
		-- VALUES('usp_update_user_email', v_errno, v_sqlstate, v_msg, NOW());
	END;
	START TRANSACTION;

    # 여기서부터 시작
	CASE p_social_type
	WHEN 1 THEN -- Guest

    SET user_index = 1;

    INSERT IGNORE INTO daily_login_user_table (user_index, login_datetime)
	VALUES (user_index, CURDATE());
    
	SELECT user_index;

	COMMIT;
END$$

DELIMITER ;
;


