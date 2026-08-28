UPDATE tbl_jobticket
SET ClientFullName = '', PrimaryNumber = '', SecondaryNumber = NULL
WHERE JobType = 'Maintenance';

UPDATE tbl_jobticketreschedulehistory
SET Reason = '';

select * from tbl_jobticket

select * from tbl_useraccount

