CREATE TABLE `video_bgm` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `emotion_type` int(11) NOT NULL,
  `name` varchar(45) NOT NULL,
  `path` varchar(45) NOT NULL,
  `add_time` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8mb4;

CREATE TABLE `video_orgin` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `orientate_type` int(11) NOT NULL,
  `name` varchar(300) NOT NULL,
  `path` varchar(200) NOT NULL,
  `tag` varchar(200) NOT NULL,
  `add_time` datetime NOT NULL,
  `status` int(11) NOT NULL DEFAULT '1',
  `duration` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8mb4;

CREATE TABLE `video_pool` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `tag` varchar(45) NOT NULL,
  `url` varchar(200) NOT NULL,
  `local_path` varchar(100) DEFAULT NULL,
  `downloaded` int(11) NOT NULL DEFAULT '0',
  `duration` int(11) NOT NULL DEFAULT '0',
  `width` int(11) NOT NULL DEFAULT '0',
  `height` int(11) NOT NULL DEFAULT '0',
  `video_id` int(11) NOT NULL,
  `video_file_id` int(11) NOT NULL,
  `page` int(11) NOT NULL,
  `status` int(11) DEFAULT NULL,
  `add_time` datetime NOT NULL,
  `web_type` int(11) NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_video_id` (`web_type`,`video_id`),
  KEY `ix_tag` (`tag`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8mb4;


CREATE TABLE `video_result` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `orgin_id` int(11) NOT NULL,
  `words_id` int(11) NOT NULL,
  `bgm_id` int(11) NOT NULL,
  `path` varchar(45) NOT NULL,
  `add_time` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8mb4;

CREATE TABLE `video_words` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `emotion_type` int(11) NOT NULL,
  `content` varchar(500) NOT NULL,
  `add_time` datetime NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB  DEFAULT CHARSET=utf8mb4;

ALTER TABLE  `video_words` 
ADD COLUMN `bgm_id` INT NOT NULL DEFAULT 0 AFTER `add_time`;

SELECT * FROM video_result;
SELECT * FROM video_orgin;
SELECT * FROM video_words;
SELECT * FROM video_bgm order by name;
select * from video_pool  where tag='autumn' and web_type=1 and video_id=5479715;

CREATE TABLE `video_voice` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `create_time` datetime NOT NULL,
  `path` varchar(105) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=99 DEFAULT CHARSET=utf8mb4;

ALTER TABLE `video_result` 
ADD COLUMN `voice_id` INT(11) NOT NULL AFTER `bgm_id`;
