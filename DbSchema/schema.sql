

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
	IF p_social_type = 1 AND (p_social_id IS NULL or p_social_id = "")THEN
		#게스트 계정 생성 요청
		create_loop :LOOP 
			SET @BASE = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890';
			SET @GUEST_ID = '';
			
			SET @GUEST_ID_SIZE = 10;
			SET @i = 0;

			create_inner_loop :LOOP 
			   IF (@GUEST_ID_SIZE <= @i) THEN
				  LEAVE create_inner_loop;
			   END IF;  
			   SET @i = @i + 1;
			   SET @GUEST_ID = CONCAT(@GUEST_ID, substring(@BASE, CEIL(RAND() * CHAR_LENGTH(@BASE)), 1));
			END LOOP;
            
            IF NOT EXISTS (SELECT * FROM account_social_info_table WHERE social_type = p_social_type AND social_user_id = @GUEST_ID FOR UPDATE) THEN
				SET p_social_id = @GUEST_ID;
                
                INSERT account_info_table VALUE ();
    
				INSERT account_social_info_table(user_index, social_type, social_user_id, social_email)
				VALUE (LAST_INSERT_ID(), p_social_type, p_social_id, p_email);
        
				LEAVE create_loop;
            END IF;
		END LOOP;
	END IF;
    
	IF p_social_type != 1 AND NOT EXISTS (SELECT * FROM account_social_info_table WHERE social_type = p_social_type AND social_user_id = p_social_id) THEN
		INSERT account_info_table VALUE ();
    
		INSERT account_social_info_table(user_index, social_type, social_user_id, social_email)
        VALUE (LAST_INSERT_ID(), p_social_type, p_social_id, p_email);
    END IF;

	SELECT A.user_index, account_type, last_login_datetime, social_user_id, login_count
    INTO @user_index, @account_type, @last_login_datetime, @social_user_id, @login_count
    FROM account_info_table AS A INNER JOIN account_social_info_table AS B ON A.user_index = B.user_index
    WHERE social_type = in_social_type AND social_user_id = in_social_user_id FOR UPDATE;

    SET user_index = 1;

    INSERT IGNORE INTO daily_login_user_table (user_index, login_datetime)
	VALUES (user_index, CURDATE());
    
	SELECT user_index;

	COMMIT;
END$$

DELIMITER ;
;


